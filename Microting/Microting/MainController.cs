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

using eFormRequest;
using eFormSqlController;

using System;
using System.Collections.Generic;
using System.IO;

namespace Microting
{
    class MainController
    {
        #region var
        public Core core;
        #endregion

        #region con
        public MainController()
        {
            //DOSOMETHING: Change to your needs
            #region read settings
            string[] lines = File.ReadAllLines("Input.txt");

            string comToken = lines[0];
            string comAddress = lines[1];

            string subscriberToken = lines[3];
            string subscriberAddress = lines[4];
            string subscriberName = lines[5];

            string serverConnectionString = lines[7];
            int userId = int.Parse(lines[8]);

            string fileLocation = lines[10];
            #endregion

            core = new Core(comToken, comAddress, subscriberToken, subscriberAddress, subscriberName, serverConnectionString, userId, fileLocation);
            #region connect event triggers
            core.HandleCaseCreated += EventCaseCreated;
            core.HandleCaseRetrived += EventCaseRetrived;
            core.HandleCaseUpdated += EventCaseUpdated;
            core.HandleCaseDeleted += EventCaseDeleted;
            core.HandleFileDownloaded += EventFileDownloaded;
            core.HandleSiteActivated += EventSiteActivated;
            core.HandleEventLog += EventLog;
            core.HandleEventWarning += EventWarning;
            core.HandleEventException += EventException;
            #endregion
            core.Start();
        }
        #endregion

        #region public
        public int  TemplatCreate(string xmlString, string caseType)
        {
            try
            {
                return core.TemplatCreate(xmlString, caseType);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public int  TemplatCreate(MainElement mainElement, string caseType)
        {
            try
            {
                return core.TemplatCreate(mainElement, caseType);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void TemplatCreateInfinityCase(MainElement mainElement, string caseType, List<int> siteIds, int instances)
        {
            try
            {

                if (mainElement.Repeated != 0)
                    throw new Exception("InfinityCase are always Repeated = 0");

                int templatId = TemplatCreate(mainElement, caseType);

                foreach (int siteId in siteIds)
                {
                    for (int i = 0; i < instances; i++)
                    {
                        List<int> siteShortList = new List<int>();
                        siteShortList.Add(siteId);

                        core.CaseCreate(mainElement, "", siteShortList, true);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void CaseCreate(int templatId, string caseUId)
        {
            try
            {
                MainElement mainElement = core.TemplatRead(templatId);
                mainElement.PushMessageTitle = "";
                mainElement.PushMessageBody = "";
                mainElement.SetStartDate(DateTime.Now);
                mainElement.SetEndDate(DateTime.Now.AddDays(2));

                List<int> siteIds = new List<int>();
                siteIds.Add(1311);

                core.CaseCreate(mainElement, caseUId, siteIds, false);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void CaseRead(string muuId)
        {
            try
            {
                ReplyElement replyElement = core.CaseRead(muuId);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void CaseReadFromGroup(string caseUId)
        {
            try
            {
                ReplyElement replyElement = core.CaseReadAllSites(caseUId);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void CaseDelete(string muuId)
        {
            try
            {
                core.CaseDelete(muuId);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void CaseDeleteAll(string caseUId)
        {
            try
            {
                int deletedCases = core.CaseDeleteAllSites(caseUId);
            }
            catch (Exception ex)
            {
                EventLog(ex.ToString(), EventArgs.Empty);

                //DOSOMETHING: Handle the expection
                throw new NotImplementedException();
            }
        }

        public void Close()
        {
            core.Close();
        }
        #endregion

        #region events
        public void EventCaseCreated(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Case_Dto temp = (Case_Dto)sender;
            string caseUid = temp.CaseUId;
            string muuId = temp.MicrotingUId;
            int siteId = temp.SiteId;
        }

        public void EventCaseRetrived(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Case_Dto temp = (Case_Dto)sender;
            string caseUid = temp.CaseUId;
            string muuId = temp.MicrotingUId;
            int siteId = temp.SiteId;
        }

        public void EventCaseUpdated(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Case_Dto temp = (Case_Dto)sender;
            string caseUid = temp.CaseUId;
            string muuId = temp.MicrotingUId;
            int siteId = temp.SiteId;
        }

        public void EventCaseDeleted(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Case_Dto temp = (Case_Dto)sender;
            string caseUid = temp.CaseUId;
            string muuId = temp.MicrotingUId;
            int siteId = temp.SiteId;
        }

        public void EventFileDownloaded(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            File_Dto temp = (File_Dto)sender;
            string caseUid = temp.CaseUId;
            string muuId = temp.MicrotingUId;
            int siteId = temp.SiteId;
            string fileLocation = temp.FileLocation;
        }

        public void EventSiteActivated(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            int siteId = int.Parse(sender.ToString());
        }

        public void EventLog(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Console.WriteLine(sender.ToString());
        }

        public void EventWarning(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Console.WriteLine("## WARNING ## " + sender.ToString() + " ## WARNING ##");
        }

        public void EventException(object sender, EventArgs args)
        {
            //DOSOMETHING: changed to fit your wishes and needs 
            Exception ex = (Exception)sender;
        }
        #endregion
    }
}