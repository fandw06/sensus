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
using System.Collections.Generic;
using SensusService;
using System.Threading;
using HealthKit;
using Foundation;
using SensusService.Probes.User.Health;

namespace Sensus.iOS.Probes.User.Health
{
    public class iOSHealthKitBiologicalSexProbe : iOSHealthKitCharacteristicProbe
    {
        protected override string DefaultDisplayName
        {
            get
            {
                return "Biological Sex (HealthKit)";
            }
        }

        public override Type DatumType
        {
            get
            {
                return typeof(BiologicalSexDatum);
            }
        }

        public override int DefaultPollingSleepDurationMS
        {
            get
            {
                return int.MaxValue;
            }
        }

        public override HKObjectType ObjectType
        {
            get
            {
                return HKObjectType.GetCharacteristicType(HKCharacteristicTypeIdentifierKey.BiologicalSex);
            }
        }

        public iOSHealthKitBiologicalSexProbe()
        {
        }

        protected override IEnumerable<Datum> Poll(CancellationToken cancellationToken)
        {
            List<Datum> data = new List<Datum>();

            NSError error;
            HKBiologicalSexObject biologicalSex = HealthStore.GetBiologicalSex(out error);

            if (error == null)
            {
                if (biologicalSex.BiologicalSex == HKBiologicalSex.Female)
                    data.Add(new BiologicalSexDatum(DateTimeOffset.Now, BiologicalSex.Female));
                else if (biologicalSex.BiologicalSex == HKBiologicalSex.Male)
                    data.Add(new BiologicalSexDatum(DateTimeOffset.Now, BiologicalSex.Male));
                else if (biologicalSex.BiologicalSex == HKBiologicalSex.Other)
                    data.Add(new BiologicalSexDatum(DateTimeOffset.Now, BiologicalSex.Other));
            }
            else
                SensusServiceHelper.Get().Logger.Log("Error reading biological sex:  " + error.Description, LoggingLevel.Normal, GetType());
            
            return data;
        }
    }
}