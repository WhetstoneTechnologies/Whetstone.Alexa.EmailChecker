// Copyright (c) 2018 Whetstone Technologies. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using Whetstone.Alexa.CanFulfill;
using System.Text;

namespace Whetstone.Alexa.EmailChecker
{
    public enum RequestTypeEnum
    {               
        Unknown = 0,
        Launch = 1,
        Begin = 2,
        Resume = 3,
        Stop = 4,
        Pause = 5,
        Intent = 6,
        Help = 7,
        Reprompt = 8,
        Repeat = 9,
        CanFulfillIntent = 10
    }


    public class RequestRecordMessage
    {

        public RequestRecordMessage()
        {
            Client = "Alexa";
            TitleId = "emailaddresschecker";
            TitleVersion = "1.0";
        }


        public RequestRecordMessage(AlexaRequest req, long duration) : this()
        {

            string sessionId = req?.Session?.SessionId;
            string userId = req?.Session?.User?.UserId;
            string locale = req?.Request?.Locale;
            string requestId = req?.Request?.RequestId;
            string intentName = req?.Request?.Intent?.Name;

            DateTime? selectionTime = req?.Request?.Timestamp;

            Alexa.RequestType? alexaRequestType = req?.Request?.Type;

            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString() : sessionId;
            UserId = string.IsNullOrWhiteSpace(userId) ? Guid.NewGuid().ToString() : userId;
            Locale = locale;
            RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString() : requestId;
            IntentName = intentName;
            IsNewSession = req?.Session?.New;
            ProcessDuration = duration;

            SelectionTime = selectionTime.GetValueOrDefault(DateTime.UtcNow);

            if (alexaRequestType.HasValue)
            {
                switch(alexaRequestType.Value)
                {
                    case Alexa.RequestType.CanFulfillIntentRequest:
                        RequestType = RequestTypeEnum.CanFulfillIntent;
                        break;
                    case Alexa.RequestType.IntentRequest:
                        RequestType = RequestTypeEnum.Intent;
                        break;
                    case Alexa.RequestType.LaunchRequest:
                        RequestType = RequestTypeEnum.Launch;
                        break;
                    default:
                        RequestType = RequestTypeEnum.Unknown;
                        break;
                }

            }
            else
                RequestType = RequestTypeEnum.Unknown;

        }


        public RequestRecordMessage(AlexaRequest req, long duration, CanFulfillEnum canFulfill) : this(req, duration)
        {
            CanFulfill = canFulfill;
        }

        /// <summary>
        /// This is the session id related to the connecting service.
        /// </summary>
        /// <remarks>If this connecting app is Alexa, then this is the Alexa session id.</remarks>

        [JsonProperty(PropertyName = "sessionId")]
        [JsonRequired]
        public string SessionId { get; set; }
        
        [JsonProperty(PropertyName = "client")]
        public string Client { get; set; }

        /// <summary>
        /// The user id provided by the connecting service. It is possible the user id is missing if request is a CanFulfill request.
        /// </summary>
        /// <remarks>If the connected service is Alexa, then this is the Alexa user id.</remarks>
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }

        [JsonProperty(PropertyName = "locale")]
        public string Locale { get; set; }

        [JsonProperty(PropertyName = "titleId", NullValueHandling = NullValueHandling.Ignore)]
        public string TitleId { get; set; }

        [JsonProperty(PropertyName = "titleVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string TitleVersion { get; set; }

        [JsonProperty(PropertyName = "requestId", NullValueHandling = NullValueHandling.Ignore)]
        public string RequestId { get; set; }

        [JsonProperty(PropertyName = "intentName", NullValueHandling = NullValueHandling.Ignore)]
        public string IntentName { get; set; }

        [JsonProperty(PropertyName = "slots", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Slots { get; set; }

        [JsonRequired]
        [JsonProperty(PropertyName = "time")]
        public DateTime SelectionTime { get; set; }


        [JsonProperty(PropertyName = "isNewSession", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsNewSession { get; set; }

        [JsonProperty(PropertyName = "processDuration")]
        public long ProcessDuration { get; set; }

        [JsonProperty(PropertyName = "preNodeActionLog", NullValueHandling = NullValueHandling.Ignore)]
        public string PreNodeActionLog { get; set; }

        [JsonProperty(PropertyName = "postNodeActionLog", NullValueHandling = NullValueHandling.Ignore)]
        public string PostNodeActionLog { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "requestType")]
        public RequestTypeEnum RequestType { get; set; }


        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "canFulfill", NullValueHandling = NullValueHandling.Ignore)]
        public CanFulfillEnum? CanFulfill { get; set; }

     
    }
}
