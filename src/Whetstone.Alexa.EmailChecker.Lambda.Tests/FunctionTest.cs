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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json;
using System.Diagnostics;
using Whetstone.Alexa.Display;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Whetstone.Alexa.Security;
using Whetstone.Alexa.ProgressiveResponse;
using Whetstone.Aws.Sqs;
using Whetstone.Alexa.EmailChecker.Configuration;
using Microsoft.Extensions.Options;

namespace Whetstone.Alexa.EmailChecker.Lambda.Tests
{
    public class FunctionTest
    {

        internal const string IMAGEPATH_CONFIG = "ImagePathRoot";
        internal const string LOG_LEVEL_CONFIG = "LogLevel";
        internal const string REDISSERVER_CONFIG = "RedisServer";
        internal const string REDISSERVERINTANCE_CONFIG = "RedisServerInstance";
        internal const string SQSQUEUENAME_CONFIG = "SqsQueueName";
        internal const string SQSQUEUEURL_CONFIG = "SqsQueueUrl";

        private string DEFAULT_SESSIONQUEUENAME = "dev-sessionqueue";

        public FunctionTest()
        {


        }

        [Trait("Type", "UnitTest")]
        [Fact]
        public async Task FireCanFulfillRequestTest()
        {
            AlexaRequest req = GenerateRequest(RequestType.CanFulfillIntentRequest);

            req.Request.Intent = new IntentAttributes("EmailCheckIntent");

            IServiceProvider serProv = GetTestProvider();

            var function = new Function(serProv);
            var context = new TestLambdaContext();

           AlexaResponse resp = await function.FunctionHandlerAsync(req, context);


            Assert.Equal(CanFulfill.CanFulfillEnum.Yes, resp.Response.CanFulfillIntent.CanFulfill);

            string jsonText = JsonConvert.SerializeObject(resp);

        }



        [Trait("Type", "UnitTest")]
        [Fact]
        public async Task FireLaunchRequestTest()
        {
            AlexaRequest req = GenerateRequest(RequestType.LaunchRequest);
            IServiceProvider serProv = GetTestProvider();
            var function = new Function(serProv);
            var context = new TestLambdaContext();

             AlexaResponse resp = null;

            try
            {
                resp = await function.FunctionHandlerAsync(req, context);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }

            // No email returned
            Assert.Contains("The email address checker needs permission", resp.Response.OutputSpeech.Text);

            // No directives should be returned since the request does not support the Echo Show
            Assert.Null(resp.Response.Directives);
        }


        [Trait("Type", "UnitTest")]
        [Fact]
        public async Task FirePermittedLaunchRequestTest()
        {
            AlexaRequest req = GenerateRequest(RequestType.LaunchRequest);
            req.Context.System.ApiAccessToken = "GOODTOKEN";
            IServiceProvider serProv = GetTestProvider();
            var function = new Function(serProv);
            var context = new TestLambdaContext();

            AlexaResponse resp = null;

            try
            {
                resp = await function.FunctionHandlerAsync(req, context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }

            // No email returned
            Assert.Contains("myaddress@email.com", resp.Response.Card.Content);

            // No directives should be returned since the request does not support the Echo Show
            Assert.Null(resp.Response.Directives);
        }

        [Trait("Type", "UnitTest")]
        [Fact]
        public async Task FirePingTest()
        {
            AlexaRequest req = new AlexaRequest();
            req.Version = "ping";
            AlexaResponse resp;
            IServiceProvider serProv = GetTestProvider();

            var function = new Function(serProv);
            var context = new TestLambdaContext();

            try
            {
                resp = await function.FunctionHandlerAsync(req, context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }


            // No directives should be returned since the request does not support the Echo Show
            Assert.NotNull(resp);
        }


        [Trait("Type", "UnitTest")]
        [Fact]
        public async Task FireLaunchRequestWithDisplayTest()
        {

            AlexaRequest req = GenerateRequest(RequestType.LaunchRequest);
            req.Context.System.Device.SupportedInterfaces.Display = new DisplayInterfaceAttributes();
            string urlRoot = "https://dev-custom.s3.amazonaws.com/emailchecker/image/";

            IServiceProvider serProv = GetTestProvider(urlRoot);

            var function = new Function(serProv);
            var context = new TestLambdaContext();

            AlexaResponse resp = null;

            try
            {
                resp = await function.FunctionHandlerAsync(req, context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw;
            }

            // No email returned
            Assert.Contains("The email address checker needs permission", resp.Response.OutputSpeech.Text);

            // No directives should be returned since the request does not support the Echo Show
            Assert.NotNull(resp.Response.Directives);



            DisplayDirectiveResponse displayResp = (DisplayDirectiveResponse) resp.Response.Directives.FirstOrDefault(x => x.Type.Equals("Display.RenderTemplate"));

            DisplayDirectiveImageSource backImage =  displayResp.Template.BackgroundImage.Sources.First();

            Assert.Contains(urlRoot, backImage.Url);

        }

        private IServiceProvider GetTestProvider()
        {

            return GetTestProvider("https://dev-custom.s3.amazonaws.com/emailchecker/image/");
        }


        private IServiceProvider GetTestProvider(string imageRoot)
        {
            EmailCheckerConfig checkerConfig = new EmailCheckerConfig(imageRoot);

            var serviceProvider = new Mock<IServiceProvider>();


            IOptions<EmailCheckerConfig> mockConfig = Options.Create(new EmailCheckerConfig(imageRoot));


            serviceProvider
                .Setup(x => x.GetService(typeof(IOptions<EmailCheckerConfig>)))
                .Returns(mockConfig);

            ILogger<EmailProcessor> emailLogger = Mock.Of<ILogger<EmailProcessor>>();

            serviceProvider
                .Setup(x => x.GetService(typeof(ILogger<EmailProcessor>)))
                .Returns(emailLogger);


            Mock<IAlexaUserDataManager> userManagerStub= new Mock<IAlexaUserDataManager>();


            userManagerStub
                .Setup( x=>  x.GetAlexaUserEmailAsync("https://api.amazonalexa.com", "SOMETOKEN"))
                .Returns( () =>
                    {

                        throw new AlexaSecurityException("Forbidden", System.Net.HttpStatusCode.Forbidden, new Exception());
                    });


            userManagerStub
                .Setup(x => x.GetAlexaUserEmailAsync("https://api.amazonalexa.com", "GOODTOKEN"))
                .Returns(() =>
                {
                    return Task.FromResult<string>("myaddress@email.com");
                });

            IAlexaUserDataManager userManager = userManagerStub.Object;


            serviceProvider
                .Setup(x => x.GetService(typeof(IAlexaUserDataManager)))
                .Returns(userManager);

            IProgressiveResponseManager progMan = Mock.Of<IProgressiveResponseManager>();

            serviceProvider
                .Setup(x => x.GetService(typeof(IProgressiveResponseManager)))
                .Returns(progMan);

            ISqsService sqsService = Mock.Of<ISqsService>();
            serviceProvider
                .Setup(x => x.GetService(typeof(ISqsService)))
                .Returns(sqsService);


            IEmailProcessor emailProcessor = new EmailProcessor(mockConfig, emailLogger, userManager, progMan, sqsService);

            serviceProvider
                .Setup(x => x.GetService(typeof(IEmailProcessor)))
                .Returns(emailProcessor);

            return serviceProvider.Object;

            //public EmailProcessor(IOptions<EmailCheckerConfig> emailCheckerConfig,
            //                        ILogger<EmailProcessor> logger,
            //                        IAlexaUserDataManager userDataManager,
            //                        IProgressiveResponseManager progMan,
            //                        ISqsService sqsService
            //                      )

        }





        private AlexaRequest GenerateRequest(RequestType reqType)
        {


            AlexaRequest req = new AlexaRequest();

            req.Version = "1.0";

            req.Request = new RequestAttributes()
            {
                RequestId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now,
                Locale = "en-US",
                Type = reqType
            };

            req.Session = new AlexaSessionAttributes()
            {
                New = true,
                SessionId = Guid.NewGuid().ToString()
            };

            req.Context = new ContextAttributes()
            {
                System = new SystemAttributes()
                {
                    ApiEndpoint = "https://api.amazonalexa.com",
                    ApiAccessToken = "SOMETOKEN",
                    Device = new DeviceAttributes()
                    {
                        SupportedInterfaces = new SupportedInterfacesAttributes()
                    }
                }
            };

            return req;
        }


    }


}
