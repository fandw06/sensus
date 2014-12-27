using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using SensusService.Probes.Movement;
using Android.Hardware;
using Java.Lang;

namespace Sensus.Android.Probes.Movement
{
    public class AndroidAccelerometerProbe : AccelerometerProbe
    {
        private AndroidSensorListener _accelerometerListener;
        private float[] _gravity;

        public AndroidAccelerometerProbe()
        {
            _gravity = new float[3];

            _accelerometerListener = new AndroidSensorListener(SensorType.Accelerometer, SensorDelay.Normal, null, e =>
                {
                    if (e.Values.Count != 3)
                        return;

                    // http://developer.android.com/guide/topics/sensors/sensors_motion.html#sensors-motion-accel

                    float alpha = 0.8f;

                    _gravity[0] = alpha * _gravity[0] + (1 - alpha) * e.Values[0];
                    _gravity[1] = alpha * _gravity[1] + (1 - alpha) * e.Values[1];
                    _gravity[2] = alpha * _gravity[2] + (1 - alpha) * e.Values[2];

                    float xAccel = e.Values[0] - _gravity[0];
                    float yAccel = e.Values[1] - _gravity[1];
                    float zAccel = e.Values[2] - _gravity[2];

                    StoreDatum(new AccelerometerDatum(this, DateTimeOffset.UtcNow, xAccel, yAccel, zAccel));
                });
        }

        protected override bool Initialize()
        {
            return base.Initialize() && _accelerometerListener.Initialize();
        }

        public override void StartListening()
        {
            _accelerometerListener.Start();
        }

        public override void StopListening()
        {
            _accelerometerListener.Stop();
        }
    }
}