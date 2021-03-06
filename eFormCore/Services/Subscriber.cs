﻿/*
The MIT License (MIT)

Copyright (c) 2014 microting

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using eFormShared;
using eFormSqlController;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.Runtime;
using Amazon;
using System.Threading;
using Newtonsoft.Json.Linq;
using eForm.Messages;
using Rebus.Bus;

namespace eFormSubscriber
{
    public class Subscriber
    {
        #region var
        SqlController sqlController;
        Log log;
        private readonly IBus bus;
        bool keepSubscribed;
        bool isActive;
        Thread subscriberThread;
        Tools t = new Tools();
        #endregion

        #region con
        /// <summary>
        /// Microting notification server subscriber C# DLL.
        /// </summary>
        public Subscriber(SqlController sqlController, Log log, IBus bus)
        {
            this.sqlController = sqlController;
            this.log = log;
            this.bus = bus;
        }
        #endregion

        #region public
        /// <summary>
        /// Starts a Notification Subscriber to Microting
        /// </summary>
        public void Start()
        {
            if (!isActive)
            {
                subscriberThread = new Thread(() => SubscriberThread());
                subscriberThread.Start();

                int tries = 0;
                while (!isActive)
                {
                    Thread.Sleep(100);
                    tries++;

                    if (tries > 100)
                        throw new Exception("Failed to start Subscriber after 10 secs");
                }
            }
        }

        /// <summary>
        /// Sends the close command to the Notification Subscriber
        /// </summary>
        public void Close()
        {
            try
            {
                keepSubscribed = false;
                int tries = 0;

                while (isActive)
                {
                    if (tries > 100) //25 secs
                        subscriberThread.Abort();

                    Thread.Sleep(250);
                    tries++;
                }
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", t.GetMethodName() + " failed", ex, false);
            }
        }

        /// <summary>
        /// Tells if the Notification Subscriber to Microting is running.
        /// </summary>
        public bool IsActive()
        {
            return isActive;
        }
        #endregion

        #region private
        private void SubscriberThread()
        {
            if (sqlController.SettingRead(Settings.token) != "UNIT_TEST___________________L:32")
            #region amazon
            {
                #region setup
                isActive = true;
                keepSubscribed = true;

                string awsAccessKeyId = sqlController.SettingRead(Settings.awsAccessKeyId);
                string awsSecretAccessKey = sqlController.SettingRead(Settings.awsSecretAccessKey);
                string awsQueueUrl = sqlController.SettingRead(Settings.awsEndPoint) + sqlController.SettingRead(Settings.token);

                var sqsClient = new AmazonSQSClient(awsAccessKeyId, awsSecretAccessKey, RegionEndpoint.EUCentral1);
                DateTime lastExpection = DateTime.MinValue;
                DateTime lastCheckAdd15s;
                #endregion

                while (keepSubscribed)
                {
                    try
                    {
                        lastCheckAdd15s = DateTime.Now.AddSeconds(15);
                        var res = sqsClient.ReceiveMessage(awsQueueUrl);

                        if (res.Messages.Count > 0)
                            foreach (var message in res.Messages)
                            {
                                #region JSON -> var
                                var parsedData = JRaw.Parse(message.Body);
                                string notificationUId = parsedData["id"].ToString();
                                string microtingUId = parsedData["microting_uuid"].ToString();
                                string action = parsedData["text"].ToString();
                                #endregion
                                log.LogStandard("Not Specified", "Notification notificationUId : " + notificationUId + " microtingUId : " + microtingUId + " action : " + action);
                                switch (action)
                                {
                                    case Constants.Notifications.RetrievedForm:
                                        bus.SendLocal(new EformRetrieved(notificationUId, microtingUId)).Wait();
                                        break;
                                    case Constants.Notifications.Completed:
                                        bus.SendLocal(new EformCompleted(notificationUId, microtingUId)).Wait();
                                        break;
                                }



                                //sqlController.NotificationCreate(notificationUId, microtingUId, action);

                                sqsClient.DeleteMessage(awsQueueUrl, message.ReceiptHandle);
                            }
                        else
                            while (lastCheckAdd15s > DateTime.Now)
                                Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        //Log expection
                        log.LogWarning("Not Specified", t.PrintException(t.GetMethodName() + " failed", ex));

                        if (DateTime.Compare(lastExpection.AddMinutes(5), DateTime.Now) > 0)
                        {
                            keepSubscribed = false;
                            log.LogException("Not Specified", t.GetMethodName() + " failed, twice in the last 5 minuts", ex, true);
                        }

                        lastExpection = DateTime.Now;
                    }
                }

                //EventMsgClient("Subscriber closed", null);
                keepSubscribed = false;
                isActive = false;
            }
            #endregion
            else
            #region unit test
            {
                log.LogStandard("Not Specified", "Subscriber faked");
                isActive = true;
                keepSubscribed = true;

                while (keepSubscribed)
                    Thread.Sleep(100);

                keepSubscribed = false;
                isActive = false;
            }
            #endregion
        }
        #endregion
    }
}