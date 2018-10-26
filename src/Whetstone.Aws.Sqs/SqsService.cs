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
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Whetstone.Aws.Sqs
{
    public class SqsService : ISqsService
    {

        private ILogger<SqsService> _logger;

        private SqsConfiguration _queueConfig;

        private AsyncLazy<string> _queueUrl = null;

        private IDistributedCache _cache;

        private const string QUEUE_CACHE_KEY = "sqsqueueurl:{0}";

        public SqsService(ILogger<SqsService> logger, IOptions<SqsConfiguration> queueConfig, IDistributedCache distCache)
        {
            if (queueConfig?.Value == null)
                throw new ArgumentNullException("Queue configuration not provided");
            _queueConfig = queueConfig.Value;

            if (string.IsNullOrWhiteSpace(_queueConfig.QueueName) && string.IsNullOrWhiteSpace(_queueConfig.QueueUrl))
                throw new ArgumentException("queueConfig must set either the QueueName or the QueueUrl");


            _cache = distCache;

            _logger = logger;

            _queueUrl = new AsyncLazy<string>(() => { return GetQueueUrlAsync(); });
        }

        private async Task<string> GetQueueUrlAsync()
        {

            string queueUrl = null;

            if (string.IsNullOrWhiteSpace(_queueConfig.QueueUrl))
            {

                _queueConfig.QueueUrl = await GetCacheQueueUrlAsync(_queueConfig.QueueName);
                if (string.IsNullOrWhiteSpace(_queueConfig.QueueUrl))
                {
                    try
                    {
                        using (AmazonSQSClient sqsClient = GetSqsService())
                        {
                            GetQueueUrlResponse queueNameResp = await sqsClient.GetQueueUrlAsync(_queueConfig.QueueName);
                            _queueConfig.QueueUrl = queueNameResp.QueueUrl;

                            _logger.LogInformation($"Queue name {_queueConfig.QueueName} resolved to queue url {queueUrl}");
                        }
                    }
                    catch (QueueDoesNotExistException ex)
                    {
                        throw new QueueDoesNotExistException($"Url for queue name '{_queueConfig.QueueName}' not found", ex);
                    }

                    await SetCacheQueueUrlAsync(_queueConfig);
                }
             

            }

            return _queueConfig.QueueUrl;
        }


        private async Task<string> GetCacheQueueUrlAsync(string queueName)
        {

            string cachedQueueUrl = await _cache.GetStringAsync(string.Format(QUEUE_CACHE_KEY, queueName));

            return cachedQueueUrl;
        }

        private async Task SetCacheQueueUrlAsync(SqsConfiguration sqsConfig)
        {

            DistributedCacheEntryOptions distOptions = new DistributedCacheEntryOptions();

            distOptions.SlidingExpiration = new TimeSpan(2, 0, 0);

            await _cache.SetStringAsync(string.Format(QUEUE_CACHE_KEY, sqsConfig.QueueName), 
                sqsConfig.QueueUrl, 
                distOptions);
        }


        public async Task SendMessageAsync<T>(T message)
        {

            string messageText = JsonConvert.SerializeObject(message);


            SendMessageRequest sendRequest = new SendMessageRequest();
            sendRequest.QueueUrl = await _queueUrl.Value;
            sendRequest.MessageBody = messageText;
            try
            {
                using (AmazonSQSClient sqsClient = GetSqsService())
                {
                    SendMessageResponse sendResponse = await sqsClient.SendMessageAsync(sendRequest);

                    //_logger.LogInformation("Session log sent in Message id {0} to queue with sessionId {1} and requestId {2} to queue url {3} and received https status code {4}",
                    //  sendResponse.MessageId,
                    //  sessionLogMessage.SessionId,
                    //  sessionLogMessage.RequestId,
                    //  sendRequest.QueueUrl,
                    //  sendResponse.HttpStatusCode);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error sending session log to queue {sendRequest.QueueUrl}: {messageText}", ex);
            }
        }


        private AmazonSQSClient GetSqsService()
        {
            var regionConfig = RegionEndpoint.GetBySystemName(_queueConfig.AwsRegion);
            return new AmazonSQSClient(regionConfig);
        }

    }
}
