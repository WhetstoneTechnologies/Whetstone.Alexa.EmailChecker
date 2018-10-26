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
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Whetstone.Alexa.Display;
using Whetstone.Alexa.Security;
using Whetstone.Alexa.CanFulfill;
using Whetstone.Alexa.ProgressiveResponse;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Whetstone.Alexa.EmailChecker.Configuration;
using Whetstone.Aws.Sqs;

namespace Whetstone.Alexa.EmailChecker
{


    public class EmailProcessor : IEmailProcessor
    {



        internal const string EMAIL_REQUEST_INTENT = "EmailCheckIntent";



        private ILogger<EmailProcessor> _logger;
        private IAlexaUserDataManager _userDataManager;
        private  IProgressiveResponseManager _progMan;
        private IOptions<EmailCheckerConfig> _emailCheckerConfig;
        private ISqsService _sqsService;


        public EmailProcessor(IOptions<EmailCheckerConfig> emailCheckerConfig,
                                ILogger<EmailProcessor> logger, 
                                IAlexaUserDataManager userDataManager, 
                                IProgressiveResponseManager progMan, 
                                ISqsService sqsService
                              )
        {
            _userDataManager = userDataManager;
            _logger = logger;
            _progMan = progMan;
            _emailCheckerConfig = emailCheckerConfig;
            _sqsService = sqsService;
        }

        public async Task<AlexaResponse> ProcessEmailRequestAsync(AlexaRequest request)
        {
            Stopwatch functionDuration = new Stopwatch();
            functionDuration.Start();
            AlexaResponse response = new AlexaResponse();
            bool isPing = false;
            string requestLogInfo = null;

            if (request.Version.Equals("ping"))
            {
                requestLogInfo = "Ping request";
                _logger.LogInformation(requestLogInfo);
                isPing = true;
            }
            else
            {
             
                switch (request.Request.Type)
                {
                    case RequestType.SkillPermissionAccepted:
                         requestLogInfo = "Received skill permission accepted event";
                        _logger.LogInformation(requestLogInfo);

#if DEBUG
                        if (request.HasAcceptedPermission(PermissionScopes.SCOPE_EMAIL_READ))
                        {
                            // get the user's email.
                            var emailResult = await GetEmailAddressAsync(request);

                            if (emailResult.PermissionGranted)
                            {
                                string emailAddress = emailResult.Email;

                                requestLogInfo = $"Email permission requested. Found email {emailAddress}";
                                _logger.LogInformation(requestLogInfo);
                            }
                        }
#endif
                        response = new AlexaResponse();
                        break;
                    case RequestType.SkillPermissionChanged:
                        requestLogInfo = "Received skill permission changed event";
                        _logger.LogInformation(requestLogInfo);
                        response = new AlexaResponse();
                        break;
                    case RequestType.SkillAccountLinked:
                        requestLogInfo = "Received skill account linked event";
                        _logger.LogInformation(requestLogInfo);
                        response = new AlexaResponse();
                        break;
                    case RequestType.SkillDisabled:
                        requestLogInfo = "Received skill disabled event";
                        _logger.LogInformation(requestLogInfo);
                        response = new AlexaResponse();
                        break;
                    case RequestType.SkillEnabled:
                        requestLogInfo = "Received skill enabled event";
                        _logger.LogInformation(requestLogInfo);
                        response = new AlexaResponse();
                        break;
                    case RequestType.LaunchRequest:
                        requestLogInfo = "Processing launch request";
                        _logger.LogInformation(requestLogInfo);
                        response = await GetEmailResponseAsync(request);
                        break;
                    case RequestType.IntentRequest:
                        requestLogInfo = "Processing intent request";
                        _logger.LogInformation(requestLogInfo);
                        response = await GetEmailResponseAsync(request);
                        break;
                    case RequestType.CanFulfillIntentRequest:
                        requestLogInfo = "Processing CanFulfill request";
                        _logger.LogInformation(requestLogInfo);
                        response = await GetCanFulfillResponse(request.Request.Intent);
                        break;
                }
            }


            functionDuration.Stop();
            if (!isPing)
            {
                RequestRecordMessage recMessage = request.Request?.Type == RequestType.CanFulfillIntentRequest ?
                    new RequestRecordMessage(request,
                        functionDuration.ElapsedMilliseconds,
                        response.Response.CanFulfillIntent.CanFulfill) :
                    new RequestRecordMessage(request, functionDuration.ElapsedMilliseconds);

                recMessage.PreNodeActionLog = requestLogInfo;

                await _sqsService.SendMessageAsync(recMessage);
            }

            _logger.LogInformation($"Function duration: {functionDuration.ElapsedMilliseconds}");

            return response;
        }

        private string GetImageUrl(string imageFile)
        {
            if (string.IsNullOrWhiteSpace(imageFile))
                throw new ArgumentException("imageFile cannot be null or empty");

            if (_emailCheckerConfig?.Value == null)
                throw new Exception("Configuration manager not set");

            string imagePath = _emailCheckerConfig.Value.ImageRootPath;

            if (string.IsNullOrWhiteSpace(imagePath))
                throw new Exception($"Image configuration path value missing");

            imagePath = imagePath.Trim();

            if(imagePath[imagePath.Length-1] != '/')
            {
                imagePath = string.Concat(imagePath, "/"); 
            }

            imagePath = string.Concat(imagePath, imageFile);

            return imagePath;
        }

        private async  Task<AlexaResponse> GetCanFulfillResponse(IntentAttributes intent)
        {
            AlexaResponse resp = new AlexaResponse();
            resp.Version = "1.0";
            resp.Response = new AlexaResponseAttributes();

            if(intent.Name.Equals(EMAIL_REQUEST_INTENT, StringComparison.OrdinalIgnoreCase))
            {
                resp.Response.CanFulfillIntent = new CanFulfillResponseAttributes(CanFulfillEnum.Yes);
            }
            else
            {
                resp.Response.CanFulfillIntent = new CanFulfillResponseAttributes(CanFulfillEnum.No);
            }
            //resp.Response.ShouldEndSession = true;

            return resp;
        }

        private async Task<AlexaResponse> GetEmailResponseAsync(AlexaRequest req)
        {

            string emailAddress = null;

            AlexaResponse resp = new AlexaResponse
            {
                Version = "1.0",

                Response = new AlexaResponseAttributes
                {
                    OutputSpeech = new OutputSpeechAttributes()
                }
            };


            var ret = await GetEmailAddressAsync(req);
            bool supportsDisplay = req.Context?.System?.Device?.SupportedInterfaces?.Display != null;

            if (ret.PermissionGranted)
            {
                try
                {

                    await _progMan.SendProgressiveResponseAsync(req,
                        "I'm working on it");
                }
                catch(Exception ex)
                {
                    // Log the error, don't fail the call
                    _logger.LogError(ex, "Error sending progressive response");

                }

                resp.Response.OutputSpeech.Type = OutputSpeechType.Ssml;

                string[] emailParts = ret.Email.Split('@');

                string emailName = emailParts[0];
                string emailHost = emailParts[1];

                string title = "Your Email";

                resp.Response.OutputSpeech.Ssml = $"<speak>Your email is <prosody rate=\"x-slow\"><say-as interpret-as=\"characters\">{emailName}</say-as></prosody>@{emailHost}</speak>";

                string textResp = ret.Email;

                resp.Response.Card = CardBuilder.GetSimpleCardResponse(title, string.Concat("Your email is ", ret.Email));

        

                if (supportsDisplay)
                {
                    resp.Response.Directives = new List<DirectiveResponse>();
                    DisplayDirectiveResponse displayResp = new DisplayDirectiveResponse();
                    displayResp.Template = new DisplayTemplate();
                    displayResp.Template.Type = DisplayTemplateTypeEnum.BodyTemplate3;
                    displayResp.Template.Token = "user_email";
                    displayResp.Template.Title = title;
                    displayResp.Template.TextContent = new DisplayTextContent();
                    displayResp.Template.TextContent.PrimaryText = new DisplayTextField();
                    displayResp.Template.TextContent.PrimaryText.Type = DisplayTextTypeEnum.RichText;
                    displayResp.Template.TextContent.PrimaryText.Text = "<b><font size=\"6\">" + WebUtility.HtmlEncode(ret.Email) + "</font></b>";

                    displayResp.Template.Image = GetSideIconImage();


                    displayResp.Template.BackgroundImage = GetBackgroundImage();

                    resp.Response.Directives.Add(displayResp);
                }



            }
            else
            {
                resp.Response.OutputSpeech.Type = OutputSpeechType.PlainText;

                StringBuilder sb = new StringBuilder();
                sb.Append("The email address checker needs permission to access your email in order to repeat it to you.  ");                
                sb.Append("Your email address is not retained or used by the skill other than to repeat the email to you.");

                string textResp = sb.ToString();
                resp.Response.OutputSpeech.Text = sb.ToString();

                resp.Response.Card = CardBuilder.GetPermissionRequestCard(PersonalDataType.Email);


                if (supportsDisplay)
                {
                    DisplayDirectiveResponse displayResponse = new DisplayDirectiveResponse(DisplayTemplateTypeEnum.BodyTemplate3, "Permission Needed");

                    displayResponse.Template.BackgroundImage = GetBackgroundImage();
                    displayResponse.Template.Token = "no_permission";


                    DisplayTextContent displayText = new DisplayTextContent();
                    displayText.PrimaryText = new DisplayTextField("The skill needs permission to access your email.");
                    displayText.PrimaryText.Type = DisplayTextTypeEnum.RichText;
                    displayText.SecondaryText = new DisplayTextField("A permission request has been sent to your Alexa mobile app. You can check it on your mobile phone or open a browser and log into alexa.amazon.com");

                    displayResponse.Template.TextContent = displayText;
                    displayResponse.Template.Image = GetSideIconImage();


                    DisplayDirectiveHintResponse hintResponse = new DisplayDirectiveHintResponse("Check alexa.amazon.com or your Alexa mobile app.");


                    resp.Response.Directives = new List<DirectiveResponse>();
                    resp.Response.Directives.Add(displayResponse);
                    resp.Response.Directives.Add(hintResponse);
                }
              
            }
            
            resp.Response.ShouldEndSession = true;
            return resp;
        }

        private DisplayDirectiveImage GetSideIconImage()
        {
            DisplayDirectiveImage sideImage = new DisplayDirectiveImage("email icon");


            DisplayDirectiveImageSource emailIcon576 = new DisplayDirectiveImageSource(
                GetImageUrl("emailicon_576x576.png"), 576, 576);

            DisplayDirectiveImageSource emailIcon340 = new DisplayDirectiveImageSource(
                GetImageUrl("emailicon_340x340.png"), 340, 340);

            sideImage.Sources = new List<DisplayDirectiveImageSource> { emailIcon340, emailIcon576 };


            return sideImage;

        }

        private async Task<(bool PermissionGranted, string Email)> GetEmailAddressAsync(AlexaRequest req)
        {
            string emailAddress = null;
            bool isPermissionGranted = false;

            if (_userDataManager == null)
                throw new Exception("UserDataManager not found.");

            if(string.IsNullOrWhiteSpace(req?.Context?.System?.ApiEndpoint))
                throw new ArgumentException("Context.System.ApiEndpoint in request is missing or null");



            if (string.IsNullOrWhiteSpace(req?.Context?.System?.ApiAccessToken))
                throw new ArgumentException("Context.System.ApiAccessToken in request is missing or null");

            try
            {
                emailAddress = await _userDataManager.GetAlexaUserEmailAsync(req.Context.System.ApiEndpoint, req.Context.System.ApiAccessToken);
                isPermissionGranted = true;
            }
            catch (AlexaSecurityException secEx)
            {
                isPermissionGranted = false;
                if (secEx.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger?.LogInformation("User has not granted access to their email address.");
                }
                else
                {
                    _logger.LogError($"Unexpected error getting user email: {secEx.Message}, status code {secEx.StatusCode}");
                }
            }

            return (PermissionGranted : isPermissionGranted, Email : emailAddress);
        }

        private DisplayDirectiveImage GetBackgroundImage()
        {

            DisplayDirectiveImage backImage = new DisplayDirectiveImage();
            backImage.ContentDescription = "email background";
            backImage.Sources = new List<DisplayDirectiveImageSource>();

            DisplayDirectiveImageSource imageSource = new DisplayDirectiveImageSource();
            imageSource.WidthPixels = 1200;
            imageSource.HeightPixels = 600;

            imageSource.Url = GetImageUrl("emailbackground_1200x600.png");
            //"https://dev-custom.s3.amazonaws.com/emailchecker/image/emailbackground_1200x600.png", 1024, 600);

            backImage.Sources.Add(imageSource);

            return backImage;

        }

    }

}
