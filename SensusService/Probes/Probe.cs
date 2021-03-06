// Copyright 2014 The Rector & Visitors of the University of Virginia
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Newtonsoft.Json;
using SensusUI.UiProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Syncfusion.SfChart.XForms;
using System.Collections.ObjectModel;

namespace SensusService.Probes
{
    /// <summary>
    /// An abstract probe.
    /// </summary>
    public abstract class Probe
    {
        #region static members

        public static void GetAllAsync(Action<List<Probe>> callback)
        {
            new Thread(() =>
                {
                    List<Probe> probes = null;
                    ManualResetEvent probesWait = new ManualResetEvent(false);

                    // the reflection stuff we do below (at least on android) needs to be run on the main thread.
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                        {
                            probes = Assembly.GetExecutingAssembly().GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Probe))).Select(t => Activator.CreateInstance(t) as Probe).OrderBy(p => p.DisplayName).ToList();
                            probesWait.Set();
                        });

                    probesWait.WaitOne();

                    callback(probes);

                }).Start();
        }

        #endregion

        /// <summary>
        /// Fired when the most recently sensed datum is changed, regardless of whether the datum was stored.
        /// </summary>
        public event EventHandler<Tuple<Datum, Datum>> MostRecentDatumChanged;

        private bool _enabled;
        private bool _running;
        private Datum _mostRecentDatum;
        private Protocol _protocol;
        private bool _storeData;
        private DateTimeOffset _mostRecentStoreTimestamp;
        private bool _originallyEnabled;
        private List<Tuple<bool, DateTime>> _startStopTimes;
        private List<DateTime> _successfulHealthTestTimes;
        private ObservableCollection<ChartDataPoint> _chartData;
        private int _maxChartDataCount;
        private SfChart _chart;

        private readonly object _locker = new object();

        [JsonIgnore]
        public abstract string DisplayName { get; }

        [JsonIgnore]
        public abstract string CollectionDescription { get; }

        [OnOffUiProperty("Enabled:", true, 2)]
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (value != _enabled)
                {
                    _enabled = value;

                    // _protocol can be null when deserializing the probe -- if Enabled is set before Protocol
                    if (_protocol != null && _protocol.Running)
                    {
                        if (_enabled)
                            StartAsync();
                        else
                            StopAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether or not this probe was originally enabled within the protocol. Some probes can become disabled when 
        /// attempting to start them. For example, the temperature probe might not be supported on all hardware and will thus become 
        /// disabled after its failed initialization. Thus, we need a separate variable (other than Enabled) to tell us whether the 
        /// probe was originally enabled. We use this value to calculate participation levels and also to restore the probe before 
        /// sharing it with others (e.g., since other people might have temperature hardware in their devices).
        /// </summary>
        /// <value>Whether or not this probe was enabled the first time the protocol was started.</value>
        public bool OriginallyEnabled
        {
            get
            {
                return _originallyEnabled;
            }
            set
            {
                _originallyEnabled = value;
            }
        }

        [JsonIgnore]
        public bool Running
        {
            get { return _running; }
        }

        /// <summary>
        /// Gets or sets the datum that was most recently sensed, regardless of whether the datum was stored.
        /// </summary>
        /// <value>The most recent datum.</value>
        [JsonIgnore]
        public Datum MostRecentDatum
        {
            get { return _mostRecentDatum; }
            set
            {
                if (value != _mostRecentDatum)
                {
                    Datum previousDatum = _mostRecentDatum;

                    _mostRecentDatum = value;

                    if (MostRecentDatumChanged != null)
                        MostRecentDatumChanged(this, new Tuple<Datum, Datum>(previousDatum, _mostRecentDatum));
                }
            }
        }

        [JsonIgnore]
        public DateTimeOffset MostRecentStoreTimestamp
        {
            get { return _mostRecentStoreTimestamp; }
        }

        public Protocol Protocol
        {
            get { return _protocol; }
            set { _protocol = value; }
        }

        [OnOffUiProperty("Store Data:", true, 3)]
        public bool StoreData
        {
            get { return _storeData; }
            set { _storeData = value; }
        }

        [JsonIgnore]
        public abstract Type DatumType { get; }

        [JsonIgnore]
        protected abstract float RawParticipation { get; }

        /// <summary>
        /// Gets a list of times at which the probe was started (tuple bool = True) and stopped (tuple bool = False). Only includes 
        /// those that have occurred within the protocol's participation horizon.
        /// </summary>
        /// <value>The start stop times.</value>
        public List<Tuple<bool, DateTime>> StartStopTimes
        {
            get { return _startStopTimes; }
        }

        /// <summary>
        /// Gets the successful health test times. Only includes those that have occurred within the
        /// protocol's participation horizon.
        /// </summary>
        /// <value>The successful health test times.</value>
        public List<DateTime> SuccessfulHealthTestTimes
        {
            get { return _successfulHealthTestTimes; }
        }

        [EntryIntegerUiProperty("Max Chart Data Count:", true, 50)]
        public int MaxChartDataCount
        {
            get
            {
                return _maxChartDataCount;
            }
            set
            {
                if (value > 0)
                    _maxChartDataCount = value;
            }
        }

        protected Probe()
        {
            _enabled = _running = false;
            _storeData = true;
            _startStopTimes = new List<Tuple<bool, DateTime>>();
            _successfulHealthTestTimes = new List<DateTime>();
            _chartData = new ObservableCollection<ChartDataPoint>();
            _maxChartDataCount = 500;
        }

        /// <summary>
        /// Initializes this probe. Throws an exception if initialization fails.
        /// </summary>
        protected virtual void Initialize()
        {
            _chartData.Clear();
            _mostRecentDatum = null;
            _mostRecentStoreTimestamp = DateTimeOffset.UtcNow;  // mark storage delay from initialization of probe
        }

        /// <summary>
        /// Gets the participation level for the current probe. If this probe was originally enabled within the protocol, then
        /// this will be a value between 0 and 1, with 1 indicating perfect participation and 0 indicating no participation. If 
        /// this probe was not originally enabled within the protocol, then the returned value will be null, indicating that this
        /// probe should not be included in calculations of overall protocol participation. Probes can become disabled if they
        /// are not supported on the current device or if the user refuses to initialize them (e.g., by not signing into Facebook).
        /// Although they become disabled, they were originally enabled within the protocol and participation should reflect this.
        /// Lastly, this will return null if the probe is not storing its data, as might be the case if a probe is enabled in order
        /// to trigger scripts but not told to store its data.
        /// </summary>
        /// <returns>The participation level (null, or somewhere 0-1).</returns>
        public float? GetParticipation()
        {
            if (_originallyEnabled && _storeData)
                return Math.Min(RawParticipation, 1);  // raw participations can be > 1, e.g. in the case of polling probes that the user can cause to poll repeatedly. cut off at 1 to maintain the interpretation of 1 as perfect participation.
            else
                return null;
        }

        protected void StartAsync()
        {
            new Thread(() =>
                {
                    try
                    {
                        Start();
                    }
                    catch (Exception)
                    {
                    }

                }).Start();
        }

        /// <summary>
        /// Start this instance, throwing an exception if anything goes wrong. If an exception is thrown, the caller can assume that any relevant
        /// information will have already been logged and displayed. Thus, the caller doesn't need to do anything with the exception information.
        /// </summary>
        public void Start()
        {
            try
            {
                InternalStart();
            }
            catch (Exception startException)
            {
                // stop probe to clean up any inconsistent state information
                try
                {
                    Stop();
                }
                catch (Exception stopException)
                {
                    SensusServiceHelper.Get().Logger.Log("Failed to stop probe after failing to start it:  " + stopException.Message, LoggingLevel.Normal, GetType());
                }

                string message = "Failed to start probe \"" + GetType().FullName + "\":  " + startException.Message;
                SensusServiceHelper.Get().Logger.Log(message, LoggingLevel.Normal, GetType());
                SensusServiceHelper.Get().FlashNotificationAsync(message);

                // disable probe if it is not supported on the device (or if the user has elected not to enable it -- e.g., by refusing to log into facebook)
                if (startException is NotSupportedException)
                    Enabled = false;

                throw startException;
            }
        }

        /// <summary>
        /// Throws an exception if start fails. Should be called first within child-class overrides. This should only be called within Start. This setup
        /// allows for child-class overrides, but since InternalStart is protected, it cannot be called from the outside. Outsiders only have access to
        /// Start (perhaps via Enabled), which takes care of any exceptions arising from the entire chain of InternalStart overrides.
        /// </summary>
        protected virtual void InternalStart()
        {
            lock (_locker)
            {
                if (_running)
                    SensusServiceHelper.Get().Logger.Log("Attempted to start probe, but it was already running.", LoggingLevel.Normal, GetType());
                else
                {
                    SensusServiceHelper.Get().Logger.Log("Starting.", LoggingLevel.Normal, GetType());

                    Initialize();
                    _running = true;

                    lock (_startStopTimes)
                    {
                        _startStopTimes.Add(new Tuple<bool, DateTime>(true, DateTime.Now));
                        _startStopTimes.RemoveAll(t => t.Item2 < Protocol.ParticipationHorizon);
                    }
                }
            }
        }

        public virtual void StoreDatum(Datum datum)
        {
            // datum is allowed to be null, indicating the the probe attempted to obtain data but it didn't find any (in the case of polling probes).
            if (datum != null)
            {
                datum.ProtocolId = Protocol.Id;

                if (_storeData)
                {
                    _protocol.LocalDataStore.Add(datum);

                    lock (_chartData)
                    {
                        ChartDataPoint chartDataPoint = GetChartDataPointFromDatum(datum);

                        if (chartDataPoint != null)
                        {
                            _chartData.Add(chartDataPoint);

                            while (_chartData.Count > _maxChartDataCount && _chartData.Count > 0)
                                _chartData.RemoveAt(0);
                        }
                    }
                }
            }

            MostRecentDatum = datum;
            _mostRecentStoreTimestamp = DateTimeOffset.UtcNow;  // this is outside the _storeData restriction above since we just want to track when this method is called.
        }

        protected void StopAsync()
        {
            new Thread(() =>
                {
                    try
                    {
                        Stop();
                    }
                    catch (Exception ex)
                    {
                        SensusServiceHelper.Get().Logger.Log("Failed to stop:  " + ex.Message, LoggingLevel.Normal, GetType());
                    }

                }).Start();
        }

        /// <summary>
        /// Should be called first within child-class overrides.
        /// </summary>
        public virtual void Stop()
        {
            lock (_locker)
            {
                if (_running)
                {
                    SensusServiceHelper.Get().Logger.Log("Stopping.", LoggingLevel.Normal, GetType());

                    _running = false;

                    lock (_startStopTimes)
                    {
                        _startStopTimes.Add(new Tuple<bool, DateTime>(false, DateTime.Now));
                        _startStopTimes.RemoveAll(t => t.Item2 < Protocol.ParticipationHorizon);
                    }
                }
                else
                    SensusServiceHelper.Get().Logger.Log("Attempted to stop probe, but it wasn't running.", LoggingLevel.Normal, GetType());
            }
        }

        public void Restart()
        {
            lock (_locker)
            {
                Stop();
                Start();
            }
        }

        public virtual bool TestHealth(ref string error, ref string warning, ref string misc)
        {
            bool restart = false;

            if (!_running)
            {
                restart = true;
                error += "Probe \"" + GetType().FullName + "\" is not running." + Environment.NewLine;
            }

            return restart;
        }

        public virtual void ResetForSharing()
        {
            if (_running)
                throw new Exception("Cannot clear probe while it is running.");

            lock (_chartData)
            {
                _chartData.Clear();
            }

            lock (_startStopTimes)
                _startStopTimes.Clear();

            lock (_successfulHealthTestTimes)
                _successfulHealthTestTimes.Clear();

            _mostRecentDatum = null;
            _mostRecentStoreTimestamp = DateTimeOffset.MinValue;
        }

        public SfChart GetChart()
        {
            ChartSeries series = GetChartSeries();

            if (series == null)
                return null;
            else if (_chart == null)
            {
                _chart = new SfChart
                {
                    PrimaryAxis = GetChartPrimaryAxis(),
                    SecondaryAxis = GetChartSecondaryAxis()
                };

                series.ItemsSource = _chartData;
                series.EnableAnimation = true;
                _chart.Series.Add(series);

                _chart.ChartBehaviors.Add(new ChartZoomPanBehavior
                {
                    EnablePanning = true,
                    EnableZooming = true,
                    EnableDoubleTap = true
                });
            }

            return _chart;
        }

        protected abstract ChartSeries GetChartSeries();

        protected abstract ChartAxis GetChartPrimaryAxis();

        protected abstract RangeAxisBase GetChartSecondaryAxis();

        protected abstract ChartDataPoint GetChartDataPointFromDatum(Datum datum);
    }
}