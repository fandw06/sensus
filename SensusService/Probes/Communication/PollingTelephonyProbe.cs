﻿// Copyright 2014 The Rector & Visitors of the University of Virginia
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

using System;
using SensusService.Probes;
using SensusService.Probes.Communication;
using Syncfusion.SfChart.XForms;

namespace SensusService
{
    public abstract class PollingTelephonyProbe : PollingProbe
    {
        public sealed override string DisplayName
        {
            get { return "Phone Call"; }
        }

        public override string CollectionDescription
        {
            get
            {
                return DisplayName + ":  When calls are made.";
            }
        }

        public override int DefaultPollingSleepDurationMS
        {
            get
            {
                return 60000 * 60; // once per hour
            }
        }

        public sealed override Type DatumType
        {
            get { return typeof(TelephonyDatum); }
        }

        protected override ChartSeries GetChartSeries()
        {
            return null;
        }

        protected override ChartDataPoint GetChartDataPointFromDatum(Datum datum)
        {
            return null;
        }

        protected override ChartAxis GetChartPrimaryAxis()
        {
            throw new NotImplementedException();
        }

        protected override RangeAxisBase GetChartSecondaryAxis()
        {
            throw new NotImplementedException();
        }
    }
}