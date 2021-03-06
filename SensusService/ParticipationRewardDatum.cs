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

namespace SensusService
{
    public class ParticipationRewardDatum : Datum
    {
        private float _participation;

        public float Participation
        {
            get
            {
                return _participation;
            }
            set
            {
                _participation = value;
            }
        }

        public override string DisplayDetail
        {
            get
            {
                return "Participation:  " + _participation;       
            }
        }

        public ParticipationRewardDatum(DateTimeOffset timestamp, float participation)
            : base(timestamp)
        {
            _participation = participation;
        }

        public override string ToString()
        {
            return base.ToString() + Environment.NewLine +
            "Participation:  " + _participation;
        }
    }
}