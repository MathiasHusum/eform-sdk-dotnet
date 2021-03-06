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

using eFormCommunicator;
using eFormData;
using eFormShared;
using eFormSubscriber;
using eFormSqlController;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Net;
using System.Security.Cryptography;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Xml;
using OfficeOpenXml;
using Castle.Windsor;
using eFormCore.Installers;
using Castle.MicroKernel.Registration;
using Rebus.Bus;

namespace eFormCore
{
    public class Core : CoreBase, ICore
    {
        #region events
        public event EventHandler HandleCaseCreated;
        public event EventHandler HandleCaseRetrived;
        public event EventHandler HandleCaseCompleted;
        public event EventHandler HandleCaseDeleted;
        public event EventHandler HandleFileDownloaded;
        public event EventHandler HandleSiteActivated;
        public event EventHandler HandleNotificationNotFound;
        public event EventHandler HandleEventException;
        #endregion

        #region var
        Subscriber subscriber;
        Communicator communicator;
        SqlController sqlController;
        Tools t = new Tools();

        IWindsorContainer container;

        public Log log;

        object _lockMain = new object();
        object _lockEventMessage = new object();

        bool updateIsRunningFiles = false;
        bool updateIsRunningNotifications = false;
        bool updateIsRunningTables = false;
        bool updateIsRunningEntities = false;

        bool coreThreadRunning = false;
        bool coreRestarting = false;
        bool coreStatChanging = false;
        bool coreAvailable = false;

        bool skipRestartDelay = false;

        string connectionString;
        string fileLocationPicture;
        string fileLocationPdf;

        int sameExceptionCountTried = 0;
        #endregion

        //con

        #region public state
        public bool Start(string connectionString)
        {
            try
            {
                if (!coreAvailable && !coreStatChanging)
                {


                    if (!StartSqlOnly(connectionString))
                        return false;
                    log.LogCritical("Not Specified", t.GetMethodName() + " called");

                    //---

                    container = new WindsorContainer();
                    container.Register(Component.For<SqlController>().Instance(sqlController));
                    container.Install(
                        new RebusHandlerInstaller()
                        , new RebusInstaller(connectionString)
                    );

                    coreStatChanging = true;

                    //subscriber
                    var bus = container.Resolve<IBus>();
                    subscriber = new Subscriber(sqlController, log, bus);
                    subscriber.Start();
                    log.LogStandard("Not Specified", "Subscriber started");

                    log.LogCritical("Not Specified", t.GetMethodName() + " started");
                    coreAvailable = true;
                    coreStatChanging = false;

                    //coreThread
                    Thread coreThread = new Thread(() => CoreThread());
                    coreThread.Start();
                    log.LogStandard("Not Specified", "CoreThread started");
                }
            }
            #region catch
            catch (Exception ex)
            {
                FatalExpection(t.GetMethodName() + " failed", ex);
                return false;
            }
            #endregion

            return true;
        }

        public bool StartSqlOnly(string connectionString)
        {
            try
            {
                if (!coreAvailable && !coreStatChanging)
                {
                    coreStatChanging = true;

                    if (string.IsNullOrEmpty(connectionString))
                        throw new ArgumentException("serverConnectionString is not allowed to be null or empty");

                    //sqlController
                    sqlController = new SqlController(connectionString);

                    //check settings
                    if (sqlController.SettingCheckAll().Count > 0)
                        throw new ArgumentException("Use AdminTool to setup database correctly. 'SettingCheckAll()' returned with errors");

                    if (sqlController.SettingRead(Settings.token) == "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX")
                        throw new ArgumentException("Use AdminTool to setup database correctly. Token not set, only default value found");

                    if (sqlController.SettingRead(Settings.firstRunDone) != "true")
                        throw new ArgumentException("Use AdminTool to setup database correctly. FirstRunDone has not completed");

                    if (sqlController.SettingRead(Settings.knownSitesDone) != "true")
                        throw new ArgumentException("Use AdminTool to setup database correctly. KnownSitesDone has not completed");

                    //log
                    if (log == null)
                        log = sqlController.StartLog(this);

                    log.LogCritical("Not Specified", "###########################################################################");
                    log.LogCritical("Not Specified", t.GetMethodName() + " called");
                    log.LogStandard("Not Specified", "SqlController and Logger started");

                    //settings read
                    this.connectionString = connectionString;
                    fileLocationPicture = sqlController.SettingRead(Settings.fileLocationPicture);
                    fileLocationPdf = sqlController.SettingRead(Settings.fileLocationPdf);
                    log.LogStandard("Not Specified", "Settings read");

                    //communicators
                    communicator = new Communicator(sqlController, log);
                    log.LogStandard("Not Specified", "Communicator started");

                    log.LogCritical("Not Specified", t.GetMethodName() + " started");
                    coreAvailable = true;
                    coreStatChanging = false;
                }
            }
            #region catch
            catch (Exception ex)
            {
                coreThreadRunning = false;
                coreStatChanging = false;

                FatalExpection(t.GetMethodName() + " failed", ex);
                return false;
            }
            #endregion

            return true;
        }

        public override void Restart(int sameExceptionCount, int sameExceptionCountMax)
        {
            try
            {
                if (coreRestarting == false)
                {
                    coreRestarting = true;
                    log.LogCritical("Not Specified", t.GetMethodName() + " called");
                    log.LogVariable("Not Specified", nameof(sameExceptionCount), sameExceptionCount);
                    log.LogVariable("Not Specified", nameof(sameExceptionCountMax), sameExceptionCountMax);

                    sameExceptionCountTried++;

                    if (sameExceptionCountTried > sameExceptionCountMax)
                        sameExceptionCountTried = sameExceptionCountMax;

                    if (sameExceptionCountTried > 4)
                        throw new Exception("The same Exception repeated to many times (5+) within one hour");

                    int secondsDelay = 0;
                    switch (sameExceptionCountTried)
                    {
                        case 1: secondsDelay = 001; break;
                        case 2: secondsDelay = 008; break;
                        case 3: secondsDelay = 064; break;
                        case 4: secondsDelay = 512; break;
                        default: throw new ArgumentOutOfRangeException("sameExceptionCount should be above 0");
                    }
                    log.LogVariable("Not Specified", nameof(sameExceptionCountTried), sameExceptionCountTried);
                    log.LogVariable("Not Specified", nameof(secondsDelay), secondsDelay);

                    Close();

                    log.LogStandard("Not Specified", "Trying to restart the Core in " + secondsDelay + " seconds");

                    if (!skipRestartDelay)
                        Thread.Sleep(secondsDelay * 1000);
                    else
                        log.LogStandard("Not Specified", "Delay skipped");

                    Start(connectionString);
                    coreRestarting = false;
                }
            }
            catch (Exception ex)
            {
                FatalExpection(t.GetMethodName() + "failed. Core failed to restart", ex);
            }
        }

        public bool Close()
        {
            try
            {
                if (coreAvailable && !coreStatChanging)
                {
                    lock (_lockMain) //Will let sending Cases sending finish, before closing
                    {
                        coreStatChanging = true;

                        coreAvailable = false;
                        log.LogCritical("Not Specified", t.GetMethodName() + " called");

                        try
                        {
                            if (subscriber != null)
                            {
                                log.LogEverything("Not Specified", "Subscriber requested to close connection");
                                subscriber.Close();
                                log.LogEverything("Not Specified", "Subscriber closed");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogException("Not Specified", "Subscriber failed to close", ex, false);
                        }

                        int tries = 0;
                        while (coreThreadRunning)
                        {
                            Thread.Sleep(100);
                            tries++;

                            if (tries > 600)
                                FatalExpection("Failed to close Core correct after 60 secs", new Exception());
                        }

                        updateIsRunningFiles = false;
                        updateIsRunningNotifications = false;
                        updateIsRunningTables = false;
                        updateIsRunningEntities = false;

                        log.LogStandard("Not Specified", "Core closed");
                        subscriber = null;
                        communicator = null;
                        sqlController = null;

                        coreStatChanging = false;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //"Even if you handle it, it will be automatically re-thrown by the CLR at the end of the try/catch/finally."
                Thread.ResetAbort(); //This ends the re-throwning
            }
            catch (Exception ex)
            {
                FatalExpection(t.GetMethodName() + " failed. Core failed to close", ex);
            }
            return true;
        }

        public bool Running()
        {
            return coreAvailable;
        }

        public void FatalExpection(string reason, Exception exception)
        {
            coreAvailable = false;
            coreThreadRunning = false;
            coreStatChanging = false;
            coreRestarting = false;

            try
            {
                log.LogFatalException(t.GetMethodName() + " called for reason:'" + reason + "'", exception);
            }
            catch { }

            try { HandleEventException?.Invoke(exception, EventArgs.Empty); } catch { }
            throw new Exception("FATAL exception, Core shutting down, due to:'" + reason + "'", exception);
        }
        #endregion

        #region public actions
        #region template
        public MainElement TemplateFromXml(string xmlString)
        {
            string methodName = t.GetMethodName();
            try
            {
                log.LogStandard("Not Specified", methodName + " called");
                log.LogEverything("Not Specified", "XML to transform:");
                log.LogEverything("Not Specified", xmlString);

                //XML HACK TODO
                #region xmlString = corrected xml if needed
                xmlString = xmlString.Trim();
                xmlString = xmlString.Replace("=\"choose_entity\">", "=\"EntitySearch\">");
                xmlString = xmlString.Replace("=\"single_select\">", "=\"SingleSelect\">");
                xmlString = xmlString.Replace("=\"multi_select\">", "=\"MultiSelect\">");


                xmlString = t.ReplaceInsensitive(xmlString, "<main", "<Main");
                xmlString = t.ReplaceInsensitive(xmlString, "</main", "</Main");

                xmlString = t.ReplaceInsensitive(xmlString, "<element", "<Element");
                xmlString = t.ReplaceInsensitive(xmlString, "</element", "</Element");

                xmlString = t.ReplaceInsensitive(xmlString, "<dataItem", "<DataItem");
                xmlString = t.ReplaceInsensitive(xmlString, "</dataItem", "</DataItem");

                List<string> keyWords = new List<string>();
                keyWords.Add("GroupElement");
                keyWords.Add("DataElement");

                keyWords.Add("Audio");
                keyWords.Add("CheckBox");
                keyWords.Add("Comment");
                keyWords.Add("Date");
                keyWords.Add("EntitySearch");
                keyWords.Add("EntitySelect");
                keyWords.Add("None");
                keyWords.Add("Number");
                keyWords.Add("MultiSelect");
                keyWords.Add("Picture");
                keyWords.Add("ShowPdf");
                keyWords.Add("SaveButton");
                keyWords.Add("Signature");
                keyWords.Add("SingleSelect");
                keyWords.Add("Text");
                keyWords.Add("Timer");

                foreach (var item in keyWords)
                    xmlString = t.ReplaceInsensitive(xmlString, "=\"" + item + "\">", "=\"" + item + "\">");

                xmlString = xmlString.Replace("<Main>", "<Main xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
                xmlString = xmlString.Replace("<Element type=", "<Element xsi:type=");
                xmlString = xmlString.Replace("<DataItem type=", "<DataItem xsi:type=");
                //xmlString = xmlString.Replace("<DataItemGroup type=", "<DataItemGroup xsi:type=");
                xmlString = xmlString.Replace("FieldGroup", "FieldContainer");
                xmlString = xmlString.Replace("<DataItemGroup type=", "<DataItem xsi:type=");
                xmlString = xmlString.Replace("</DataItemGroup>", "</DataItem>");
                xmlString = xmlString.Replace("<DataItemGroupList>", "<DataItemList>");
                xmlString = xmlString.Replace("</DataItemGroupList>", "</DataItemList>");

                xmlString = xmlString.Replace("<FolderName>", "<CheckListFolderName>");
                xmlString = xmlString.Replace("</FolderName>", "</CheckListFolderName>");

                xmlString = xmlString.Replace("=\"ShowPDF\">", "=\"ShowPdf\">");
                xmlString = xmlString.Replace("=\"choose_entity\">", "=\"EntitySearch\">");

                string temp = t.Locate(xmlString, "<DoneButtonDisabled>", "</DoneButtonDisabled>");
                if (temp == "false")
                {
                    xmlString = xmlString.Replace("DoneButtonDisabled", "DoneButtonEnabled");
                    xmlString = xmlString.Replace("<DoneButtonEnabled>false", "<DoneButtonEnabled>true");
                }
                if (temp == "true")
                {
                    xmlString = xmlString.Replace("DoneButtonDisabled", "DoneButtonEnabled");
                    xmlString = xmlString.Replace("<DoneButtonEnabled>true", "<DoneButtonEnabled>false");
                }

                xmlString = xmlString.Replace("<MinValue />", "<MinValue>" + long.MinValue + "</MinValue>");
                xmlString = xmlString.Replace("<MinValue/>", "<MinValue>" + long.MinValue + "</MinValue>");
                xmlString = xmlString.Replace("<MaxValue />", "<MaxValue>" + long.MaxValue + "</MaxValue>");
                xmlString = xmlString.Replace("<MaxValue/>", "<MaxValue>" + long.MaxValue + "</MaxValue>");
                xmlString = xmlString.Replace("<MaxValue></MaxValue>", "<MaxValue>" + long.MaxValue + "</MaxValue>");
                xmlString = xmlString.Replace("<MinValue></MinValue>", "<MinValue>" + long.MinValue + "</MinValue>");
                xmlString = xmlString.Replace("<DecimalCount></DecimalCount>", "<DecimalCount>0</DecimalCount>");
                xmlString = xmlString.Replace("<DecimalCount />", "<DecimalCount>" + "0" + "</DecimalCount>");
                xmlString = xmlString.Replace("<DecimalCount/>", "<DecimalCount>" + "0" + "</DecimalCount>");
                xmlString = xmlString.Replace("<DisplayOrder></DisplayOrder>", "<DisplayOrder>" + "0" + "</DisplayOrder>");

                List<string> dILst = t.LocateList(xmlString, "type=\"Date\">", "</DataItem>");
                if (dILst != null)
                {
                    foreach (var item in dILst)
                    {
                        string before = item;
                        string after = item;
                        after = after.Replace("<Value />", "<DefaultValue/>");
                        after = after.Replace("<Value/>", "<DefaultValue/>");
                        after = after.Replace("<Value>", "<DefaultValue>");
                        after = after.Replace("</Value>", "</DefaultValue>");

                        xmlString = xmlString.Replace(before, after);
                    }
                }

                xmlString = t.ReplaceAtLocationAll(xmlString, "<Id>", "</Id>", "1", false);
                xmlString = t.ReplaceInsensitive(xmlString, ">True<", ">true<");
                xmlString = t.ReplaceInsensitive(xmlString, ">False<", ">false<");
                #endregion

                log.LogEverything("Not Specified", "XML after possible corrections:");
                log.LogEverything("Not Specified", xmlString);

                MainElement mainElement = new MainElement();
                mainElement = mainElement.XmlToClass(xmlString);

                //XML HACK
                mainElement.CaseType = "";
                mainElement.PushMessageTitle = "";
                mainElement.PushMessageBody = "";
                if (mainElement.Repeated < 1)
                {
                    log.LogCritical("Not Specified", "mainElement.Repeated = 1 // enforced");
                    mainElement.Repeated = 1;
                }

                return mainElement;
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return null;
            }
        }

        public List<string> TemplateValidation(MainElement mainElement)
        {
            if (mainElement == null)
                throw new ArgumentNullException("mainElement not allowed to be null");

            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");

                    List<string> errorLst = new List<string>();
                    var dataItems = mainElement.DataItemGetAll();

                    foreach (var dataItem in dataItems)
                    {
                        #region entities
                        if (dataItem.GetType() == typeof(EntitySearch))
                        {
                            EntitySearch entitySearch = (EntitySearch)dataItem;
                            var temp = sqlController.EntityGroupRead(entitySearch.EntityTypeId.ToString());
                            if (temp == null)
                                errorLst.Add("Element entitySearch.EntityTypeId:'" + entitySearch.EntityTypeId + "' is an reference to a local unknown EntitySearch group. Please update reference");
                        }

                        if (dataItem.GetType() == typeof(EntitySelect))
                        {
                            EntitySelect entitySelect = (EntitySelect)dataItem;
                            var temp = sqlController.EntityGroupRead(entitySelect.Source.ToString());
                            if (temp == null)
                                errorLst.Add("Element entitySelect.Source:'" + entitySelect.Source + "' is an reference to a local unknown EntitySearch group. Please update reference");
                        }
                        #endregion

                        #region PDF
                        if (dataItem.GetType() == typeof(ShowPdf))
                        {
                            ShowPdf showPdf = (ShowPdf)dataItem;
                            errorLst.AddRange(PdfValidate(showPdf.Value, showPdf.Id));
                        }
                        #endregion
                    }

                    return errorLst;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public MainElement TemplateUploadData(MainElement mainElement)
        {
            if (mainElement == null)
                throw new ArgumentNullException("mainElement not allowed to be null");

            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");

                    List<string> errorLst = new List<string>();
                    var dataItems = mainElement.DataItemGetAll();

                    foreach (var dataItem in dataItems)
                    {
                        #region PDF
                        if (dataItem.GetType() == typeof(ShowPdf))
                        {
                            ShowPdf showPdf = (ShowPdf)dataItem;

                            if (PdfValidate(showPdf.Value, showPdf.Id).Count != 0)
                            {
                                try
                                {
                                    //download file
                                    string downloadPath = sqlController.SettingRead(Settings.fileLocationPdf);
                                    try
                                    {
                                        (new FileInfo(downloadPath)).Directory.Create();

                                        using (WebClient client = new WebClient())
                                        {
                                            client.DownloadFile(showPdf.Value, downloadPath + "temp.pdf");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new Exception("Download failed. Path:'" + showPdf.Value + "'", ex);
                                    }

                                    //upload file
                                    string hash = PdfUpload(downloadPath + "temp.pdf");
                                    if (hash != null)
                                    {
                                        //rename local file
                                        FileInfo FileInfo = new FileInfo(downloadPath + "temp.pdf");
                                        FileInfo.CopyTo(downloadPath + hash + ".pdf", true);
                                        FileInfo.Delete();

                                        showPdf.Value = hash;
                                    }
                                    else
                                    {
                                        throw new Exception(methodName + " hash is null for field id:'" + showPdf.Id + "'");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception(methodName + " failed, for one PDF field id:'" + showPdf.Id + "'", ex);
                                }
                            }
                        }
                        #endregion
                    }

                    return mainElement;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public int TemplateCreate(MainElement mainElement)
        {
            if (mainElement == null)
                throw new ArgumentNullException("mainElement not allowed to be null");

            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");

                    List<string> errors = TemplateValidation(mainElement);

                    if (errors == null) errors = new List<string>();
                    if (errors.Count > 0)
                        throw new Exception("mainElement failed TemplateValidation. Run TemplateValidation to see errors");

                    int templateId = sqlController.TemplateCreate(mainElement);
                    log.LogEverything("Not Specified", "Template id:" + templateId.ToString() + " created in DB");
                    return templateId;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public MainElement TemplateRead(int templateId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);

                    return sqlController.TemplateRead(templateId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool TemplateDelete(int templateId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);

                    return sqlController.TemplateDelete(templateId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public Template_Dto TemplateItemRead(int templateId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);

                    return sqlController.TemplateItemRead(templateId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<Template_Dto> TemplateItemReadAll(bool includeRemoved)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(includeRemoved), includeRemoved);

                    return TemplateItemReadAll(includeRemoved, Constants.WorkflowStates.Created, "", true, "", new List<int>());
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<Template_Dto> TemplateItemReadAll(bool includeRemoved, string siteWorkflowState, string searchKey, bool descendingSort, string sortParameter, List<int> tagIds)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(includeRemoved), includeRemoved);
                    log.LogVariable("Not Specified", nameof(searchKey), searchKey);
                    log.LogVariable("Not Specified", nameof(descendingSort), descendingSort);
                    log.LogVariable("Not Specified", nameof(sortParameter), sortParameter);
                    log.LogVariable("Not Specified", nameof(tagIds), tagIds.ToString());

                    return sqlController.TemplateItemReadAll(includeRemoved, siteWorkflowState, searchKey, descendingSort, sortParameter, tagIds);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool TemplateSetTags(int templateId, List<int> tagIds)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(tagIds), tagIds.ToString());

                    return sqlController.TemplateSetTags(templateId, tagIds);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        #region case
        public string CaseCreate(MainElement mainElement, string caseUId, int siteUid)
        {
            List<int> siteUids = new List<int>();
            siteUids.Add(siteUid);
            List<string> lst = CaseCreate(mainElement, caseUId, siteUids, "");

            try
            {
                return lst[0];
            }
            catch
            {
                return null;
            }
        }

        public List<string> CaseCreate(MainElement mainElement, string caseUId, List<int> siteUids, string custom)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    lock (_lockMain) //Will let sending Cases sending finish, before closing
                    {
                        log.LogStandard("Not Specified", methodName + " called");
                        string siteIdsStr = string.Join(",", siteUids);
                        log.LogVariable("Not Specified", nameof(caseUId), caseUId);
                        log.LogVariable("Not Specified", nameof(siteIdsStr), siteIdsStr);
                        log.LogVariable("Not Specified", nameof(custom), custom);

                        #region check input
                        DateTime start = DateTime.Parse(mainElement.StartDate.ToShortDateString());
                        DateTime end = DateTime.Parse(mainElement.EndDate.ToShortDateString());

                        if (end < DateTime.Now)
                        {
                            log.LogStandard("Not Specified", "mainElement.EndDate is set to " + end);
                            throw new ArgumentException("mainElement.EndDate needs to be a future date");
                        }

                        if (end <= start)
                        {
                            log.LogStandard("Not Specified", "mainElement.StartDat is set to " + start);
                            throw new ArgumentException("mainElement.StartDate needs to be at least the day, before the remove date (mainElement.EndDate)");
                        }

                        if (caseUId != "" && mainElement.Repeated != 1)
                            throw new ArgumentException("if caseUId can only be used for mainElement.Repeated == 1");
                        #endregion

                        //sending and getting a reply
                        List<string> lstMUId = new List<string>();

                        foreach (int siteUid in siteUids)
                        {
                            string mUId = SendXml(mainElement, siteUid);

                            if (mainElement.Repeated == 1)
                                sqlController.CaseCreate(mainElement.Id, siteUid, mUId, null, caseUId, custom, DateTime.Now);
                            else
                                sqlController.CheckListSitesCreate(mainElement.Id, siteUid, mUId);

                            Case_Dto cDto = sqlController.CaseReadByMUId(mUId);
                            //InteractionCaseUpdate(cDto);
                            try { HandleCaseCreated?.Invoke(cDto, EventArgs.Empty); }
                            catch { log.LogWarning("Not Specified", "HandleCaseCreated event's external logic suffered an Expection"); }
                            log.LogStandard("Not Specified", cDto.ToString() + " has been created");

                            lstMUId.Add(mUId);
                        }
                        return lstMUId;
                    }
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public string CaseCheck(string microtingUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(microtingUId), microtingUId);

                    Case_Dto cDto = CaseLookupMUId(microtingUId);
                    return communicator.CheckStatus(cDto.MicrotingUId, cDto.SiteUId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public ReplyElement CaseRead(string microtingUId, string checkUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(microtingUId), microtingUId);
                    log.LogVariable("Not Specified", nameof(checkUId), checkUId);

                    if (checkUId == null)
                        checkUId = "";

                    if (checkUId == "" || checkUId == "0")
                        checkUId = null;

                    cases aCase = sqlController.CaseReadFull(microtingUId, checkUId);
                    #region handling if no match case found
                    if (aCase == null)
                    {
                        log.LogWarning("Not Specified", "No case found with MuuId:'" + microtingUId + "'");
                        return null;
                    }
                    #endregion

                    int id = aCase.id;
                    log.LogEverything("Not Specified", "aCase.id:" + aCase.id.ToString() + ", found");

                    ReplyElement replyElement = sqlController.CheckRead(microtingUId, checkUId);
                    return replyElement;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public Case_Dto CaseReadByCaseId(int id)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(id), id);

                    return sqlController.CaseReadByCaseId(id);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public int? CaseReadFirstId(int? templateId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);

                    return sqlController.CaseReadFirstId(templateId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }


        public List<Case> CaseReadAll(int? templateId, DateTime? start, DateTime? end)
        {
            return CaseReadAll(templateId, start, end, "not_removed", null);
        }

        public List<Case> CaseReadAll(int? templateId, DateTime? start, DateTime? end, string workflowState, string searchKey)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(start), start);
                    log.LogVariable("Not Specified", nameof(end), end);
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);

                    return CaseReadAll(templateId, start, end, workflowState, searchKey, false, null);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public List<Case> CaseReadAll(int? templateId, DateTime? start, DateTime? end, string workflowState, string searchKey, bool descendingSort, string sortParameter)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(start), start);
                    log.LogVariable("Not Specified", nameof(end), end);
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);
                    log.LogVariable("Not Specified", nameof(descendingSort), descendingSort);
                    log.LogVariable("Not Specified", nameof(sortParameter), sortParameter);

                    return sqlController.CaseReadAll(templateId, start, end, workflowState, searchKey, descendingSort, sortParameter);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public bool CaseUpdate(int caseId, List<string> newFieldValuePairLst, List<string> newCheckListValuePairLst)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(caseId), caseId);

                    if (newFieldValuePairLst == null)
                        newFieldValuePairLst = new List<string>();

                    if (newCheckListValuePairLst == null)
                        newCheckListValuePairLst = new List<string>();

                    int id = 0;
                    string value = "";

                    foreach (string str in newFieldValuePairLst)
                    {
                        id = int.Parse(t.SplitToList(str, 0, false));
                        value = t.SplitToList(str, 1, false);
                        sqlController.FieldValueUpdate(caseId, id, value);
                    }

                    foreach (string str in newCheckListValuePairLst)
                    {
                        id = int.Parse(t.SplitToList(str, 0, false));
                        value = t.SplitToList(str, 1, false);
                        sqlController.CheckListValueStatusUpdate(caseId, id, value);
                    }

                    return true;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return false;
            }
        }

        public bool CaseDelete(int templateId, int siteUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(siteUId), siteUId);

                    return CaseDelete(templateId, siteUId, "not_removed");
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return false;
            }
        }

        public bool CaseDelete(int templateId, int siteUId, string workflowState)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(siteUId), siteUId);
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);

                    List<string> errors = new List<string>();
                    foreach (string microtingUId in sqlController.CheckListSitesRead(templateId, siteUId, workflowState))
                    {
                        if (!CaseDelete(microtingUId))
                        {
                            string error = "Failed to delete case with microtingUId: " + microtingUId;
                            errors.Add(error);
                        }
                    }
                    if (errors.Count() > 0)
                    {
                        throw new Exception(String.Join("\n", errors));
                    }
                    else
                        return true;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return false;
            }
        }

        public bool CaseDelete(string microtingUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(microtingUId), microtingUId);

                    var cDto = sqlController.CaseReadByMUId(microtingUId);
                    string xmlResponse = communicator.Delete(microtingUId, cDto.SiteUId);
                    Response resp = new Response();

                    if (xmlResponse.Contains("Error occured: Contact Microting"))
                    {
                        log.LogEverything("Not Specified", "XML response:");
                        log.LogEverything("Not Specified", xmlResponse);
                        log.LogEverything("DELETE ERROR", methodName + " failed for microtingUId: " + microtingUId);
                        return true;
                    }

                    if (xmlResponse.Contains("Error"))
                    {
                        try
                        {
                            resp = resp.XmlToClass(xmlResponse);
                            log.LogException("Not Specified", methodName + " failed", new Exception("Error from Microting server: " + resp.Value), true);
                            return false;
                        }
                        catch (Exception ex)
                        {
                            log.LogException("Not Specified", methodName + " failed", ex, true);
                            return false;
                        }
                    }

                    if (xmlResponse.Contains("Parsing in progress: Can not delete check list!</Value>"))
                        for (int i = 1; i < 7; i++)
                        {
                            Thread.Sleep(i * 200);
                            xmlResponse = communicator.Delete(microtingUId, cDto.SiteUId);
                            if (!xmlResponse.Contains("Parsing in progress: Can not delete check list!</Value>"))
                                break;
                        }

                    log.LogEverything("Not Specified", "XML response:");
                    log.LogEverything("Not Specified", xmlResponse);

                    resp = resp.XmlToClass(xmlResponse);
                    if (resp.Type.ToString() == "Success")
                    {
                        try
                        {
                            sqlController.CaseDelete(microtingUId);

                            cDto = sqlController.CaseReadByMUId(microtingUId);
                            //InteractionCaseUpdate(cDto);
                            try { HandleCaseDeleted?.Invoke(cDto, EventArgs.Empty); }
                            catch { log.LogWarning("Not Specified", "HandleCaseDeleted event's external logic suffered an Expection"); }
                            log.LogStandard("Not Specified", cDto.ToString() + " has been removed");

                            return true;
                        }
                        catch { }

                        try
                        {
                            sqlController.CaseDeleteReversed(microtingUId);

                            cDto = sqlController.CaseReadByMUId(microtingUId);
                            //InteractionCaseUpdate(cDto);
                            try { HandleCaseDeleted?.Invoke(cDto, EventArgs.Empty); }
                            catch { log.LogWarning("Not Specified", "HandleCaseDeleted event's external logic suffered an Expection"); }
                            log.LogStandard("Not Specified", cDto.ToString() + " has been removed");

                            return true;
                        }
                        catch { }
                    }
                    return false;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return false;
            }
        }

        public bool CaseDeleteResult(int caseId)
        {
            string methodName = t.GetMethodName();
            log.LogStandard("Not Specified", methodName + " called");
            log.LogVariable("Not Specified", nameof(caseId), caseId);
            try
            {
                return sqlController.CaseDeleteResult(caseId);
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return false;
            }
        }

        public bool CaseUpdateFieldValues(int id)
        {
            string methodName = t.GetMethodName();
            log.LogStandard("Not Specified", methodName + " called");
            log.LogVariable("Not Specified", nameof(id), id);
            try
            {
                return sqlController.CaseUpdateFieldValues(id);
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return false;
            }
        }

        public Case_Dto CaseLookupMUId(string microtingUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(microtingUId), microtingUId);

                    return sqlController.CaseReadByMUId(microtingUId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public Case_Dto CaseLookupCaseId(int caseId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(caseId), caseId);

                    return sqlController.CaseReadByCaseId(caseId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public List<Case_Dto> CaseLookupCaseUId(string caseUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(caseUId), caseUId);

                    return sqlController.CaseReadByCaseUId(caseUId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public int? CaseIdLookup(string microtingUId, string checkUId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(microtingUId), microtingUId);
                    log.LogVariable("Not Specified", nameof(checkUId), checkUId);

                    if (checkUId == null)
                        checkUId = "";

                    if (checkUId == "" || checkUId == "0")
                        checkUId = null;

                    cases aCase = sqlController.CaseReadFull(microtingUId, checkUId);
                    #region handling if no match case found
                    if (aCase == null)
                    {
                        log.LogWarning("Not Specified", "No case found with MuuId:'" + microtingUId + "'");
                        return -1;
                    }
                    #endregion
                    int id = aCase.id;
                    log.LogEverything("Not Specified", "aCase.id:" + aCase.id.ToString() + ", found");

                    return id;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public string CasesToExcel(int? templateId, DateTime? start, DateTime? end, string pathAndName, string customPathForUploadedData)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId.ToString());
                    log.LogVariable("Not Specified", nameof(start), start.ToString());
                    log.LogVariable("Not Specified", nameof(end), end.ToString());
                    log.LogVariable("Not Specified", nameof(pathAndName), pathAndName);
                    log.LogVariable("Not Specified", nameof(customPathForUploadedData), customPathForUploadedData);

                    List<List<string>> dataSet = GenerateDataSetFromCases(templateId, start, end, customPathForUploadedData);

                    if (dataSet == null)
                        return "";

                    using (var p = new ExcelPackage())
                    {
                        var ws = p.Workbook.Worksheets.Add("DataSet");

                        int colI = 0;
                        int rowI = 0;
                        foreach (var col in dataSet)
                        {
                            colI++;
                            rowI = 0;
                            foreach (var cell in col)
                            {
                                rowI++;
                                ws.Cells[rowI, colI].Value = cell;
                            }
                        }

                        if (!pathAndName.Contains(".xlsx"))
                            pathAndName = pathAndName + ".xlsx";

                        p.SaveAs(new FileInfo(pathAndName));
                    }

                    return Path.GetFullPath(pathAndName);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return null;
            }
        }

        public string CasesToCsv(int? templateId, DateTime? start, DateTime? end, string pathAndName, string customPathForUploadedData)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId.ToString());
                    log.LogVariable("Not Specified", nameof(start), start.ToString());
                    log.LogVariable("Not Specified", nameof(end), end.ToString());
                    log.LogVariable("Not Specified", nameof(pathAndName), pathAndName);
                    log.LogVariable("Not Specified", nameof(customPathForUploadedData), customPathForUploadedData);

                    List<List<string>> dataSet = GenerateDataSetFromCases(templateId, start, end, customPathForUploadedData);

                    if (dataSet == null)
                        return "";

                    List<string> temp;
                    string text = "";

                    for (int rowN = 0; rowN < dataSet[0].Count; rowN++)
                    {
                        temp = new List<string>();

                        foreach (List<string> lst in dataSet)
                        {
                            try
                            {
                                int.Parse(lst[rowN]);
                                temp.Add(lst[rowN]);
                            }
                            catch
                            {
                                try
                                {
                                    DateTime.Parse(lst[rowN]);
                                    temp.Add(lst[rowN]);
                                }
                                catch
                                {
                                    try
                                    {
                                        temp.Add("\"" + lst[rowN] + "\"");
                                    }
                                    catch (Exception ex2)
                                    {
                                        temp.Add(ex2.Message);
                                    }
                                }
                            }
                        }

                        text += string.Join(";", temp.ToArray()) + Environment.NewLine;
                    }

                    if (!pathAndName.Contains(".csv"))
                        pathAndName = pathAndName + ".csv";

                    File.WriteAllText(pathAndName, text.Trim(), Encoding.UTF8);
                    return Path.GetFullPath(pathAndName);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return null;
            }
        }

        public string CaseToJasperXml(int caseId, string timeStamp)
        {
            string methodName = t.GetMethodName();
            try
            {
                //if (coreRunning)
                if (true)
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(caseId), caseId.ToString());
                    log.LogVariable("Not Specified", nameof(timeStamp), timeStamp);

                    if (timeStamp == null)
                        timeStamp = DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("hhmmss");

                    //get needed data
                    Case_Dto cDto = CaseLookupCaseId(caseId);
                    ReplyElement reply = CaseRead(cDto.MicrotingUId, cDto.CheckUId);
                    string clsLst = "";
                    string fldLst = "";
                    GetChecksAndFields(ref clsLst, ref fldLst, reply.ElementList);
                    log.LogVariable("Not Specified", nameof(clsLst), clsLst);
                    log.LogVariable("Not Specified", nameof(fldLst), fldLst);

                    #region convert to jasperXml
                    string jasperXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                        + Environment.NewLine + "<root>"
                        + Environment.NewLine + "<C" + reply.Id + " case_id=\"" + caseId + "\" case_name=\"" + reply.Label + "\" serial_number=\"" + caseId + "/" + cDto.MicrotingUId + "\" check_list_status=\"approved\">"
                        + Environment.NewLine + "<worker>" + Advanced_WorkerNameRead(reply.DoneById) + "</worker>"
                        + Environment.NewLine + "<date>" + reply.DoneAt + "</date>"
                        + Environment.NewLine + "<check_date>" + reply.DoneAt + "</check_date>"
                        + Environment.NewLine + "<check_lists>"

                        + clsLst

                        + Environment.NewLine + "</check_lists>"
                        + Environment.NewLine + "<fields>"

                        + fldLst

                        + Environment.NewLine + "</fields>"
                        + Environment.NewLine + "</C" + reply.Id + ">"
                        + Environment.NewLine + "</root>";
                    log.LogVariable("Not Specified", nameof(jasperXml), jasperXml);
                    #endregion

                    //place in settings allocated placement
                    string path = sqlController.SettingRead(Settings.fileLocationJasper) + "results/" + timeStamp + "_" + caseId + ".xml";
                    //string path = sqlController.SettingRead(Settings.fileLocationJasper) + "results/" + DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("hhmmss") + "_" + caseId + ".xml";
                    Directory.CreateDirectory(sqlController.SettingRead(Settings.fileLocationJasper) + "results/");
                    File.WriteAllText(path, jasperXml.Trim(), Encoding.UTF8);
                    
                    //string path = Path.GetFullPath(locaR);
                    log.LogVariable("Not Specified", nameof(path), path);
                    return path;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return null;
            }
        }

        public string GetJasperPath()
        {
            string methodName = t.GetMethodName();
            log.LogStandard("Not Specified", methodName + " called");
            try
            {
                return sqlController.SettingRead(Settings.fileLocationJasper);
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return "N/A";
            }
        }

        public string GetHttpServerAddress()
        {
            string methodName = t.GetMethodName();
            log.LogStandard("Not Specified", methodName + " called");
            try
            {
                return sqlController.SettingRead(Settings.httpServerAddress);
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return "N/A";
            }
        }

        public bool SetHttpServerAddress(string serverAddress)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(serverAddress), serverAddress);

                    sqlController.SettingUpdate(Settings.httpServerAddress, serverAddress);
                    return true;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public string CaseToPdf(int caseId, string jasperTemplate, string timeStamp)
        {
            string methodName = t.GetMethodName();
            try
            {
                //if (coreRunning)
                if (true)
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(caseId), caseId.ToString());
                    log.LogVariable("Not Specified", nameof(jasperTemplate), jasperTemplate);

                    if (timeStamp == null)
                        timeStamp = DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("hhmmss");

                    CaseToJasperXml(caseId, timeStamp);

                    #region run jar
                    // Start the child process.
                    System.Diagnostics.Process p = new System.Diagnostics.Process();
                    // Redirect the output stream of the child process.
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    string locaJ;
                    string exepath = AppDomain.CurrentDomain.BaseDirectory;
                    if (File.Exists(exepath + "\\bin\\JasperExporter.jar"))
                    {
                        locaJ = exepath + "\\bin\\JasperExporter.jar";
                    } else
                    {
                        locaJ = sqlController.SettingRead(Settings.fileLocationJasper) + "utils\\JasperExporter.jar";
                    }

                    string locaT = sqlController.SettingRead(Settings.fileLocationJasper) + "templates\\" + jasperTemplate + "\\compact\\" + jasperTemplate + ".jrxml";
                    if (!File.Exists(locaT))
                    {
                        throw new FileNotFoundException("jrxml template was not found at " + locaT);
                    }
                    string locaC = sqlController.SettingRead(Settings.fileLocationJasper) + "results\\" + timeStamp + "_" + caseId + ".xml";

                    if (!File.Exists(locaC))
                    {
                        throw new FileNotFoundException("Case result xml was not found at " + locaC);
                    }
                    string locaR = sqlController.SettingRead(Settings.fileLocationJasper) + "results\\" + timeStamp + "_" + caseId + ".pdf";

                    string command =
                        "-Dfile.encoding=UTF-8 -jar " + locaJ +
                        " -template=\"" + locaT + "\"" +
                        " type=\"pdf\"" +
                        " -uri=\"" + locaC + "\"" +
                        " -outputFile=\"" + locaR + "\"";

                    log.LogVariable("Not Specified", nameof(command), command);
                    p.StartInfo.FileName = "java.exe";
                    p.StartInfo.Arguments = command;
                    p.Start();
                    // IF needed:
                    // Do not wait for the child process to exit before
                    // reading to the end of its redirected stream.
                    // p.WaitForExit();
                    // Read the output stream first and then wait.
                    string output = p.StandardOutput.ReadToEnd();
                    log.LogVariable("Not Specified", nameof(output), output);
                    p.WaitForExit();

                    if (output != "")
                        throw new Exception("output='" + output + "', expected to be no output. This indicates an error has happened");
                    #endregion

                    //return path
                    string path = Path.GetFullPath(locaR);
                    log.LogVariable("Not Specified", nameof(path), path);
                    return path;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, false);
                return null;
            }
        }
        #endregion

        #region site
        public Site_Dto SiteCreate(string name, string userFirstName, string userLastName, string userEmail)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(name), name);
                    log.LogVariable("Not Specified", nameof(userFirstName), userFirstName);
                    log.LogVariable("Not Specified", nameof(userLastName), userLastName);
                    log.LogVariable("Not Specified", nameof(userEmail), userEmail);

                    Tuple<Site_Dto, Unit_Dto> siteResult = communicator.SiteCreate(name);

                    string token = sqlController.SettingRead(Settings.token);
                    int customerNo = communicator.OrganizationLoadAllFromRemote(token).CustomerNo;

                    string siteName = siteResult.Item1.SiteName;
                    int siteId = siteResult.Item1.SiteId;
                    int unitUId = siteResult.Item2.UnitUId;
                    int otpCode = siteResult.Item2.OtpCode;
                    SiteName_Dto siteDto = sqlController.SiteRead(siteResult.Item1.SiteId);
                    if (siteDto == null)
                    {
                        sqlController.SiteCreate((int)siteId, siteName);
                    }
                    siteDto = sqlController.SiteRead(siteId);
                    Unit_Dto unitDto = sqlController.UnitRead(unitUId);
                    if (unitDto == null)
                    {
                        sqlController.UnitCreate(unitUId, customerNo, otpCode, siteDto.SiteUId);
                    }

                    if (string.IsNullOrEmpty(userEmail))
                    {
                        Random rdn = new Random();
                        userEmail = siteId + "." + customerNo + "@invalid.invalid";
                    }

                    Worker_Dto workerDto = Advanced_WorkerCreate(userFirstName, userLastName, userEmail);
                    Advanced_SiteWorkerCreate(siteDto, workerDto);

                    return SiteRead(siteId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public Site_Dto SiteRead(int siteId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteId), siteId);

                    return sqlController.SiteReadSimple(siteId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<Site_Dto> SiteReadAll(bool includeRemoved)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    if (includeRemoved)
                        return Advanced_SiteReadAll(null, null, null);
                    else
                        return Advanced_SiteReadAll("not_removed", null, null);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public Site_Dto SiteReset(int siteId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteId), siteId);

                    Site_Dto site = SiteRead(siteId);
                    Advanced_UnitRequestOtp((int)site.UnitId);

                    return SiteRead(siteId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool SiteUpdate(int siteId, string name, string userFirstName, string userLastName, string userEmail)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    Site_Dto siteDto = SiteRead(siteId);
                    Advanced_SiteItemUpdate(siteId, name);
                    if (String.IsNullOrEmpty(userEmail))
                    {
                        //if (String.IsNullOrEmpty)
                    }
                    Advanced_WorkerUpdate((int)siteDto.WorkerUid, userFirstName, userLastName, userEmail);
                    return true;
                }
                else
                    throw new Exception("Core is not running");

            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool SiteDelete(int siteId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    Site_Dto siteDto = SiteRead(siteId);
                    Advanced_SiteItemDelete(siteId);
                    Site_Worker_Dto siteWorkerDto = Advanced_SiteWorkerRead(null, siteId, siteDto.WorkerUid);
                    Advanced_SiteWorkerDelete(siteWorkerDto.MicrotingUId);
                    Advanced_WorkerDelete((int)siteDto.WorkerUid);
                    return true;
                }
                else
                    throw new Exception("Core is not running");

            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        #region entity
        public EntityGroup EntityGroupCreate(string entityType, string name)
        {
            try
            {
                if (Running())
                {
                    EntityGroup entityGroup = sqlController.EntityGroupCreate(name, entityType);

                    string entityGroupMUId = communicator.EntityGroupCreate(entityType, name, entityGroup.Id.ToString());

                    bool isCreated = sqlController.EntityGroupUpdate(entityGroup.Id, entityGroupMUId);

                    if (isCreated)
                        return new EntityGroup(entityGroup.Id, entityGroup.Name, entityGroup.Type, entityGroupMUId, new List<EntityItem>());
                    else
                    {
                        sqlController.EntityGroupDelete(entityGroupMUId);
                        throw new Exception("EntityListCreate failed, due to list not created correct");
                    }
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", "EntityListCreate failed", ex, true);
                throw new Exception("EntityListCreate failed", ex);
            }
        }

        public EntityGroup EntityGroupRead(string entityGroupMUId)
        {
            try
            {
                if (Running())
                {
                    while (updateIsRunningEntities)
                        Thread.Sleep(200);

                    return sqlController.EntityGroupRead(entityGroupMUId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", "EntityGroupRead failed", ex, true);
                throw new Exception("EntityGroupRead failed", ex);
            }
        }

        public bool EntityGroupUpdate(EntityGroup entityGroup)
        {
            try
            {
                if (Running())
                {
                    List<string> ids = new List<string>();
                    foreach (var item in entityGroup.EntityGroupItemLst)
                        ids.Add(item.EntityItemUId);

                    if (ids.Count != ids.Distinct().Count())
                        throw new Exception("List entityGroup.entityItemUIds are not all unique"); // Duplicates exist

                    while (updateIsRunningEntities)
                        Thread.Sleep(200);

                    bool isUpdated = communicator.EntityGroupUpdate(entityGroup.Type, entityGroup.Name, entityGroup.Id, entityGroup.EntityGroupMUId);

                    if (isUpdated)
                        sqlController.EntityGroupUpdateName(entityGroup.Name, entityGroup.EntityGroupMUId);

                    sqlController.EntityGroupUpdateItems(entityGroup);

                    CoreHandleUpdateEntityItems();
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", "EntityGroupRead failed", ex, true);
                throw new Exception("EntityGroupRead failed", ex);
            }
            return true;
        }

        public bool EntityGroupDelete(string entityGroupMUId)
        {
            try
            {
                if (Running())
                {
                    while (updateIsRunningEntities)
                        Thread.Sleep(200);

                    string type = sqlController.EntityGroupDelete(entityGroupMUId);

                    if (type != null)
                        communicator.EntityGroupDelete(type, entityGroupMUId);

                    CoreHandleUpdateEntityItems();
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", "EntityGroupDelete failed", ex, true);
                throw new Exception("EntityGroupDelete failed", ex);
            }
            return true;
        }

        public string PdfUpload(string localPath)
        {
            try
            {
                if (Running())
                {
                    string chechSum = "";
                    using (var md5 = MD5.Create())
                    {
                        using (var stream = File.OpenRead(localPath))
                        {
                            byte[] grr = md5.ComputeHash(stream);
                            chechSum = BitConverter.ToString(grr).Replace("-", "").ToLower();
                        }
                    }

                    if (communicator.PdfUpload(localPath, chechSum))
                        return chechSum;
                    else
                    {
                        log.LogWarning("Not Specified", "Uploading of PDF failed");
                        return null;
                    }
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", t.GetMethodName() + " failed", ex, true);
                throw new Exception(t.GetMethodName() + " failed", ex);
            }
        }
        #endregion
        #endregion

        #region tags
        public List<Tag> GetAllTags(bool includeRemoved)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    return sqlController.GetAllTags(includeRemoved);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public int TagCreate(string name)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    return sqlController.TagCreate(name);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }



        public bool TagDelete(int tagId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    return sqlController.TagDelete(tagId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion


        #region public advanced actions
        #region templat
        public bool Advanced_TemplateDisplayIndexChangeDb(int templateId, int newDisplayIndex)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(newDisplayIndex), newDisplayIndex);

                    return sqlController.TemplateDisplayIndexChange(templateId, newDisplayIndex);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_TemplateDisplayIndexChangeServer(int templateId, int siteUId, int newDisplayIndex)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(siteUId), siteUId);
                    log.LogVariable("Not Specified", nameof(newDisplayIndex), newDisplayIndex);

                    string respXml = null;
                    List<string> errors = new List<string>();
                    foreach (string microtingUId in sqlController.CheckListSitesRead(templateId, siteUId, "not_removed"))
                    {
                        respXml = communicator.TemplateDisplayIndexChange(microtingUId.ToString(), siteUId, newDisplayIndex);
                        Response resp = new Response();
                        resp = resp.XmlToClassUsingXmlDocument(respXml);
                        if (resp.Type != Response.ResponseTypes.Success)
                        {
                            string error = "Failed to set display index for eForm " + microtingUId + " to " + newDisplayIndex;
                            errors.Add(error);
                        }
                    }
                    if (errors.Count() > 0)
                    {
                        throw new Exception(String.Join("\n", errors));
                    }
                    return true;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_TemplateUpdateFieldIdsForColumns(int templateId, int? fieldId1, int? fieldId2, int? fieldId3, int? fieldId4, int? fieldId5, int? fieldId6, int? fieldId7, int? fieldId8, int? fieldId9, int? fieldId10)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);
                    log.LogVariable("Not Specified", nameof(fieldId1), fieldId1);
                    log.LogVariable("Not Specified", nameof(fieldId2), fieldId2);
                    log.LogVariable("Not Specified", nameof(fieldId3), fieldId3);
                    log.LogVariable("Not Specified", nameof(fieldId4), fieldId4);
                    log.LogVariable("Not Specified", nameof(fieldId5), fieldId5);
                    log.LogVariable("Not Specified", nameof(fieldId6), fieldId6);
                    log.LogVariable("Not Specified", nameof(fieldId7), fieldId7);
                    log.LogVariable("Not Specified", nameof(fieldId8), fieldId8);
                    log.LogVariable("Not Specified", nameof(fieldId9), fieldId9);
                    log.LogVariable("Not Specified", nameof(fieldId10), fieldId10);

                    return sqlController.TemplateUpdateFieldIdsForColumns(templateId, fieldId1, fieldId2, fieldId3, fieldId4, fieldId5, fieldId6, fieldId7, fieldId8, fieldId9, fieldId10);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<Field_Dto> Advanced_TemplateFieldReadAll(int templateId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(templateId), templateId);

                    return sqlController.TemplateFieldReadAll(templateId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public void Advanced_ConsistencyCheckTemplates()
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    List<int> dummyList = new List<int>();
                    List<Template_Dto> allTemplates = sqlController.TemplateItemReadAll(true, "removed", "", false, "", dummyList);
                    foreach (Template_Dto item in allTemplates)
                    {
                        foreach (SiteName_Dto site in item.DeployedSites)
                        {
                            CaseDelete(item.Id, site.SiteUId, "");
                        }
                    }
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
            }
        }
        #endregion

        #region sites
        public List<Site_Dto> Advanced_SiteReadAll(string workflowState, int? offSet, int? limit)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);
                    log.LogVariable("Not Specified", nameof(offSet), offSet.ToString());
                    log.LogVariable("Not Specified", nameof(limit), limit.ToString());

                    return sqlController.SimpleSiteGetAll(workflowState, offSet, limit);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public SiteName_Dto Advanced_SiteItemRead(int siteId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteId), siteId);

                    return sqlController.SiteRead(siteId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<SiteName_Dto> Advanced_SiteItemReadAll()
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    return sqlController.SiteGetAll();
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_SiteItemUpdate(int siteId, string name)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteId), siteId);
                    log.LogVariable("Not Specified", nameof(name), name);

                    if (sqlController.SiteRead(siteId) == null)
                        return false;

                    bool success = communicator.SiteUpdate(siteId, name);
                    if (!success)
                        return false;

                    return sqlController.SiteUpdate(siteId, name);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_SiteItemDelete(int siteId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteId), siteId);

                    bool success = communicator.SiteDelete(siteId);
                    if (!success)
                        return false;

                    return sqlController.SiteDelete(siteId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        #endregion

        #region workers
        public Worker_Dto Advanced_WorkerCreate(string firstName, string lastName, string email)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(firstName), firstName);
                    log.LogVariable("Not Specified", nameof(lastName), lastName);
                    log.LogVariable("Not Specified", nameof(email), email);

                    Worker_Dto workerDto = communicator.WorkerCreate(firstName, lastName, email);
                    int workerUId = workerDto.WorkerUId;

                    workerDto = sqlController.WorkerRead(workerDto.WorkerUId);
                    if (workerDto == null)
                    {
                        sqlController.WorkerCreate(workerUId, firstName, lastName, email);
                    }

                    return Advanced_WorkerRead(workerUId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public string Advanced_WorkerNameRead(int workerId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workerId), workerId);

                    return sqlController.WorkerNameRead(workerId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public Worker_Dto Advanced_WorkerRead(int workerId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workerId), workerId);

                    return sqlController.WorkerRead(workerId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<Worker_Dto> Advanced_WorkerReadAll(string workflowState, int? offSet, int? limit)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);
                    log.LogVariable("Not Specified", nameof(offSet), offSet.ToString());
                    log.LogVariable("Not Specified", nameof(limit), limit.ToString());

                    return sqlController.WorkerGetAll(workflowState, offSet, limit);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_WorkerUpdate(int workerId, string firstName, string lastName, string email)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workerId), workerId);
                    log.LogVariable("Not Specified", nameof(firstName), firstName);
                    log.LogVariable("Not Specified", nameof(lastName), lastName);
                    log.LogVariable("Not Specified", nameof(email), email);

                    if (sqlController.WorkerRead(workerId) == null)
                        return false;

                    bool success = communicator.WorkerUpdate(workerId, firstName, lastName, email);
                    if (!success)
                        return false;

                    return sqlController.WorkerUpdate(workerId, firstName, lastName, email);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_WorkerDelete(int workerId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workerId), workerId);

                    bool success = communicator.WorkerDelete(workerId);
                    if (!success)
                        return false;

                    return sqlController.WorkerDelete(workerId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        #region site_workers
        public Site_Worker_Dto Advanced_SiteWorkerCreate(SiteName_Dto siteDto, Worker_Dto workerDto)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", "siteId", siteDto.SiteUId);
                    log.LogVariable("Not Specified", "workerId", workerDto.WorkerUId);

                    Site_Worker_Dto result = communicator.SiteWorkerCreate(siteDto.SiteUId, workerDto.WorkerUId);
                    //var parsedData = JRaw.Parse(result);
                    //int workerUid = int.Parse(parsedData["id"].ToString());

                    Site_Worker_Dto siteWorkerDto = sqlController.SiteWorkerRead(result.WorkerUId, null, null);

                    if (siteWorkerDto == null)
                    {
                        sqlController.SiteWorkerCreate(result.MicrotingUId, siteDto.SiteUId, workerDto.WorkerUId);
                    }

                    return Advanced_SiteWorkerRead(result.WorkerUId, null, null);
                    //return null;
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public Site_Worker_Dto Advanced_SiteWorkerRead(int? siteWorkerId, int? siteId, int? workerId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(siteWorkerId), siteWorkerId.ToString());
                    log.LogVariable("Not Specified", nameof(siteId), siteId.ToString());
                    log.LogVariable("Not Specified", nameof(workerId), workerId.ToString());

                    return sqlController.SiteWorkerRead(siteWorkerId, siteId, workerId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_SiteWorkerDelete(int workerId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(workerId), workerId);

                    bool success = communicator.SiteWorkerDelete(workerId);
                    if (!success)
                        return false;

                    return sqlController.SiteWorkerDelete(workerId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        #region units
        public Unit_Dto Advanced_UnitRead(int unitId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(unitId), unitId);

                    return sqlController.UnitRead(unitId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public List<Unit_Dto> Advanced_UnitReadAll()
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");

                    return sqlController.UnitGetAll();
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }

        public Unit_Dto Advanced_UnitRequestOtp(int unitId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(unitId), unitId);

                    int otp_code = communicator.UnitRequestOtp(unitId);

                    Unit_Dto my_dto = Advanced_UnitRead(unitId);

                    sqlController.UnitUpdate(unitId, my_dto.CustomerNo, otp_code, my_dto.SiteUId);

                    return Advanced_UnitRead(unitId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                return null;
            }
        }
        #endregion

        #region fields
        public Field Advanced_FieldRead(int id)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(id), id);

                    return sqlController.FieldRead(id);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public List<FieldValue> Advanced_FieldValueReadList(int id, int instances)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(id), id);
                    log.LogVariable("Not Specified", nameof(instances), instances);

                    return sqlController.FieldValueReadList(id, instances);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        //EntityGroupList
        public EntityGroupList Advanced_EntityGroupAll(string sort, string nameFilter, int pageIndex, int pageSize, string entityType, bool desc, string workflowState)
        {
            if (entityType != "EntitySearch" && entityType != "EntitySelect")
                throw new Exception("EntityGroupAll failed. EntityType:" + entityType + " is not an known type");
            if (workflowState != "not_removed" && workflowState != "created" && workflowState != "removed")
                throw new Exception("EntityGroupAll failed. workflowState:" + workflowState + " is not an known workflow state");

            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(sort), sort);
                    log.LogVariable("Not Specified", nameof(nameFilter), nameFilter);
                    log.LogVariable("Not Specified", nameof(pageIndex), pageIndex);
                    log.LogVariable("Not Specified", nameof(pageSize), pageSize);
                    log.LogVariable("Not Specified", nameof(entityType), entityType);
                    log.LogVariable("Not Specified", nameof(desc), desc);
                    log.LogVariable("Not Specified", nameof(workflowState), workflowState);

                    return sqlController.EntityGroupAll(sort, nameFilter, pageIndex, pageSize, entityType, desc, workflowState);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }

        public bool Advanced_DeleteUploadedData(int fieldId, int uploadedDataId)
        {
            string methodName = t.GetMethodName();
            try
            {
                if (Running())
                {
                    log.LogStandard("Not Specified", methodName + " called");
                    log.LogVariable("Not Specified", nameof(fieldId), fieldId);
                    log.LogVariable("Not Specified", nameof(uploadedDataId), uploadedDataId);

                    uploaded_data uD = sqlController.GetUploadedData(uploadedDataId);

                    try
                    {
                        Directory.CreateDirectory(uD.file_location + "Deleted");
                        File.Move(uD.file_location + uD.file_name, uD.file_location + @"Deleted\" + uD.file_name);
                    }
                    catch (Exception exd)
                    {
                        log.LogException("Not Specified", methodName + " failed", exd, true);
                        throw new Exception(methodName + " failed", exd);
                    }

                    return sqlController.DeleteFile(uploadedDataId);
                }
                else
                    throw new Exception("Core is not running");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", methodName + " failed", ex, true);
                throw new Exception(methodName + " failed", ex);
            }
        }
        #endregion

        #region private
        private List<Element> ReplaceDataElementsAndDataItems(int caseId, List<Element> elementList, List<FieldValue> lstAnswers)
        {
            List<Element> elementListReplaced = new List<Element>();

            foreach (Element element in elementList)
            {
                #region if DataElement
                if (element.GetType() == typeof(DataElement))
                {
                    DataElement dataE = (DataElement)element;

                    #region replace DataItemGroups
                    foreach (var dataItemGroup in dataE.DataItemGroupList)
                    {
                        FieldContainer fG = (FieldContainer)dataItemGroup;

                        List<DataItem> dataItemListTemp = new List<DataItem>();
                        foreach (var dataItem in fG.DataItemList)
                        {
                            foreach (var answer in lstAnswers)
                            {
                                if (dataItem.Id == answer.Id)
                                {
                                    dataItemListTemp.Add(answer);
                                    break;
                                }
                            }
                        }
                        fG.DataItemList = dataItemListTemp;
                    }
                    #endregion

                    #region replace DataItems
                    List<DataItem> dataItemListTemp2 = new List<DataItem>();
                    foreach (var dataItem in dataE.DataItemList)
                    {
                        foreach (var answer in lstAnswers)
                        {
                            if (dataItem.Id == answer.FieldId)
                            {
                                dataItemListTemp2.Add(answer);
                                break;
                            }
                        }
                    }
                    dataE.DataItemList = dataItemListTemp2;
                    #endregion

                    elementListReplaced.Add(new CheckListValue(dataE, sqlController.CheckListValueStatusRead(caseId, element.Id)));
                }
                #endregion

                #region if GroupElement
                if (element.GetType() == typeof(GroupElement))
                {
                    GroupElement groupE = (GroupElement)element;

                    ReplaceDataElementsAndDataItems(caseId, groupE.ElementList, lstAnswers);

                    elementListReplaced.Add(groupE);
                }
                #endregion
            }

            return elementListReplaced;
        }

        private string SendXml(MainElement mainElement, int siteId)
        {
            log.LogEverything("Not Specified", "siteId:" + siteId + ", requested sent eForm");

            string xmlStrRequest = mainElement.ClassToXml();
            string xmlStrResponse = communicator.PostXml(xmlStrRequest, siteId);

            Response response = new Response();
            response = response.XmlToClass(xmlStrResponse);

            //if reply is "success", it's created
            if (response.Type.ToString().ToLower() == "success")
            {
                return response.Value;
            }

            throw new Exception("siteId:'" + siteId + "' // failed to create eForm at Microting // Response :" + xmlStrResponse);
        }

        private List<List<string>> GenerateDataSetFromCases(int? templateId, DateTime? start, DateTime? end, string customPathForUploadedData)
        {
            List<List<string>> dataSet = new List<List<string>>();
            List<string> colume1CaseIds = new List<string> { "Id" };
            List<int> caseIds = new List<int>();

            List<Case> caseList = sqlController.CaseReadAll(templateId, start, end, "not_removed", null, false, "");
            var template = sqlController.TemplateItemRead((int)templateId);

            if (caseList.Count == 0)
                return null;

            #region firstColumes generate
            {
                List<string> colume2 = new List<string> { "Date" };
                List<string> colume3 = new List<string> { "Time" };
                List<string> colume4 = new List<string> { "Day" };
                List<string> colume5 = new List<string> { "Week" };
                List<string> colume6 = new List<string> { "Month" };
                List<string> colume7 = new List<string> { "Year" };
                List<string> colume8 = new List<string> { "Created At" };
                List<string> colume9 = new List<string> { "Site" };
                List<string> colume10 = new List<string> { "Device User" };
                List<string> colume11 = new List<string> { "Device Id" };
                List<string> colume12 = new List<string> { "eForm Name" };

                var cal = DateTimeFormatInfo.CurrentInfo.Calendar;
                foreach (var aCase in caseList)
                {
                    DateTime time = (DateTime)aCase.DoneAt;
                    colume1CaseIds.Add(aCase.Id.ToString());
                    caseIds.Add(aCase.Id);

                    colume2.Add(time.ToString("yyyy.MM.dd"));
                    colume3.Add(time.ToString("HH:mm:ss"));
                    colume4.Add(time.DayOfWeek.ToString());
                    colume5.Add(time.Year.ToString() + "." + cal.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday));
                    colume6.Add(time.Year.ToString() + "." + time.ToString("MMMM").Substring(0, 3));
                    colume7.Add(time.Year.ToString());
                    colume8.Add(aCase.CreatedAt.Value.ToString("yyyy.MM.dd HH:mm:ss"));
                    colume9.Add(aCase.SiteName);
                    colume10.Add(aCase.WorkerName);
                    colume11.Add(aCase.UnitId.ToString());
                    colume12.Add(template.Label);
                }

                dataSet.Add(colume1CaseIds);
                dataSet.Add(colume2);
                dataSet.Add(colume3);
                dataSet.Add(colume4);
                dataSet.Add(colume5);
                dataSet.Add(colume6);
                dataSet.Add(colume7);
                dataSet.Add(colume8);
                dataSet.Add(colume9);
                dataSet.Add(colume10);
                dataSet.Add(colume11);
                dataSet.Add(colume12);
            }
            #endregion

            #region fieldValue generate
            {
                if (templateId != null)
                {
                    MainElement templateData = sqlController.TemplateRead((int)templateId);

                    List<string> lstReturn = new List<string>();
                    lstReturn = GenerateDataSetFromCasesSubSet(lstReturn, templateData.ElementList, "");

                    List<string> newRow;
                    foreach (string set in lstReturn)
                    {
                        int fieldId = int.Parse(t.SplitToList(set, 0, false));
                        string label = t.SplitToList(set, 1, false);

                        List<List<KeyValuePair>> result = sqlController.FieldValueReadAllValues(fieldId, caseIds, customPathForUploadedData);

                        if (result.Count == 1)
                        {
                            newRow = new List<string>();
                            newRow.Insert(0, label);
                            List<KeyValuePair> tempList = result[0];
                            foreach (int i in caseIds)
                            {
                                string value = "";
                                foreach (KeyValuePair KvP in tempList)
                                {
                                    if (KvP.Key == i.ToString())
                                    {
                                        value = KvP.Value;
                                    }
                                }
                                newRow.Add(value);
                            }
                            dataSet.Add(newRow);
                        }
                        else
                        {
                            int option = 0;
                            Field field = sqlController.FieldRead(fieldId);
                            foreach (var lst in result)
                            {
                                newRow = new List<string>();
                                List<KeyValuePair> fieldKvP = field.KeyValuePairList;
                                newRow.Insert(0, label + " | " + fieldKvP.ElementAt(option).Value);
                                foreach (int i in caseIds)
                                {
                                    string value = "";
                                    foreach (KeyValuePair KvP in lst)
                                    {
                                        if (KvP.Key == i.ToString())
                                        {
                                            value = KvP.Value;
                                        }
                                    }
                                    newRow.Add(value);
                                }
                                dataSet.Add(newRow);
                                option++;
                            }
                        }
                    }
                }
            }
            #endregion

            return dataSet;
        }

        private List<string> GenerateDataSetFromCasesSubSet(List<string> lstReturn, List<Element> elementList, string preLabel)
        {
            foreach (Element element in elementList)
            {
                string sep = " / ";

                #region if DataElement
                if (element.GetType() == typeof(DataElement))
                {
                    DataElement dataE = (DataElement)element;

                    if (preLabel != "")
                        preLabel = preLabel + sep + dataE.Label;

                    foreach (FieldContainer dataItemGroup in dataE.DataItemGroupList)
                    {
                        foreach (DataItem dataItem in dataItemGroup.DataItemList)
                        {
                            if (dataItem.GetType() == typeof(SaveButton))
                                continue;
                            if (dataItem.GetType() == typeof(None))
                                continue;

                            if (preLabel != "")
                                lstReturn.Add(dataItem.Id + "|" + preLabel.Remove(0, sep.Length) + sep + dataItemGroup.Label + sep + dataItem.Label);
                            else
                                lstReturn.Add(dataItem.Id + "|" + dataItemGroup.Label + sep + dataItem.Label);
                        }
                    }

                    foreach (DataItem dataItem in dataE.DataItemList)
                    {
                        if (dataItem.GetType() == typeof(SaveButton))
                            continue;
                        if (dataItem.GetType() == typeof(None))
                            continue;

                        if (preLabel != "")
                            lstReturn.Add(dataItem.Id + "|" + preLabel.Remove(0, sep.Length) + sep + dataItem.Label);
                        else
                            lstReturn.Add(dataItem.Id + "|" + dataItem.Label);
                    }
                }
                #endregion

                #region if GroupElement
                if (element.GetType() == typeof(GroupElement))
                {
                    GroupElement groupE = (GroupElement)element;

                    if (preLabel != "")
                        GenerateDataSetFromCasesSubSet(lstReturn, groupE.ElementList, preLabel + sep + groupE.Label);
                    else
                        GenerateDataSetFromCasesSubSet(lstReturn, groupE.ElementList, groupE.Label);
                }
                #endregion
            }

            return lstReturn;
        }

        private List<string> PdfValidate(string pdfString, int pdfId)
        {
            List<string> errorLst = new List<string>();

            if (pdfString.ToLower().Contains("microting.com"))
                errorLst.Add("Element showPdf.Id:'" + pdfId + "' contains an URL that points to Microting's builder temporary hosting. Indicating that it's not a proper hash");
            if (pdfString.ToLower().Contains("http") || pdfString.ToLower().Contains("https"))
                errorLst.Add("Element showPdf.Id:'" + pdfId + "' contains an HTTP or HTTPS. Indicating that it's not a proper hash");
            if (pdfString.Length != 32)
                errorLst.Add("Element showPdf.Id:'" + pdfId + "' lenght is not the correct lenght (32). Indicating that it's not a proper hash");

            if (errorLst.Count > 0)
                errorLst.Add("Element showPdf.Id:'" + pdfId + "' please check 'value' input, and consider running PdfUpload");

            return errorLst;
        }
        
        private void GetChecksAndFields(ref string clsLst, ref string fldLst, List<Element> elementLst)
        {
            string jasperFieldXml = "";
            string jasperCheckXml = "";

            foreach (Element element in elementLst)
            {
                if (element.GetType() == typeof(CheckListValue))
                {
                    CheckListValue dataE = (CheckListValue)element;

                    jasperCheckXml = jasperCheckXml
                       + Environment.NewLine + "<C" + dataE.Id + ">" + dataE.Status + "</C" + dataE.Id + ">";

                    foreach (Field field in dataE.DataItemList)
                    {
                        FieldValue answer = field.FieldValues[0];

                        jasperFieldXml = jasperFieldXml
                           + Environment.NewLine + "<F" + answer.FieldId + " name=\"" + answer.Label + "\" parent=\"" + element.Label + "\">"
                           + Environment.NewLine + "<F" + answer.FieldId + "_value field_value_id=\"" + answer.Id + "\"><![CDATA[" + (answer.ValueReadable ?? string.Empty) + "]]></F" + answer.FieldId + "_value>"
                           + Environment.NewLine + "</F" + answer.FieldId + ">";
                    }
                }

                if (element.GetType() == typeof(GroupElement))
                {
                    GroupElement groupE = (GroupElement)element;
                    GetChecksAndFields(ref clsLst, ref fldLst, groupE.ElementList);
                }
            }

            clsLst = clsLst + jasperCheckXml;
            fldLst = fldLst + jasperFieldXml;
        }
        #endregion

        #region intrepidation threads
        private void CoreThread()
        {
            coreThreadRunning = true;

            log.LogEverything("Not Specified", t.GetMethodName() + " initiated");
            while (coreAvailable)
            {
                try
                {
                    if (coreThreadRunning)
                    {
                        Thread updateFilesThread
                            = new Thread(() => CoreHandleUpdateFiles());
                        updateFilesThread.Start();

                        Thread updateNotificationsThread
                            = new Thread(() => CoreHandleUpdateNotifications());
                        updateNotificationsThread.Start();

                        //Thread updateTablesThread
                        //    = new Thread(() => CoreHandleUpdateTables());
                        //updateTablesThread.Start();

                        Thread.Sleep(2000);
                    }

                    Thread.Sleep(500);
                }
                catch (ThreadAbortException)
                {
                    log.LogWarning("Not Specified", t.GetMethodName() + " catch of ThreadAbortException");
                }
                catch (Exception ex)
                {
                    FatalExpection(t.GetMethodName() + "failed", ex);
                }
            }
            log.LogEverything("Not Specified", t.GetMethodName() + " completed");

            coreThreadRunning = false;
        }

        private void CoreHandleUpdateFiles()
        {
            try
            {
                if (!updateIsRunningFiles)
                {
                    updateIsRunningFiles = true;

                    #region update files
                    UploadedData ud = null;
                    string urlStr = "";
                    bool oneFound = true;
                    while (oneFound)
                    {
                        ud = sqlController.FileRead();
                        if (ud != null)
                            urlStr = ud.FileLocation;
                        else
                            break;

                        log.LogEverything("Not Specified", "Received file:" + ud.ToString());

                        #region finding file name and creating folder if needed
                        FileInfo file = new FileInfo(fileLocationPicture);
                        file.Directory.Create(); // If the directory already exists, this method does nothing.

                        int index = urlStr.LastIndexOf("/") + 1;
                        string fileName = ud.Id.ToString() + "_" + urlStr.Remove(0, index);
                        #endregion

                        #region download file
                        using (var client = new WebClient())
                        {
                            try
                            {
                                client.DownloadFile(urlStr, fileLocationPicture + fileName);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception("Downloading and creating fil locally failed.", ex);
                            }
                        }
                        #endregion

                        #region finding checkSum
                        string chechSum = "";
                        using (var md5 = MD5.Create())
                        {
                            using (var stream = File.OpenRead(fileLocationPicture + fileName))
                            {
                                byte[] grr = md5.ComputeHash(stream);
                                chechSum = BitConverter.ToString(grr).Replace("-", "").ToLower();
                            }
                        }
                        #endregion

                        #region checks checkSum
                        if (chechSum != fileName.Substring(fileName.LastIndexOf(".") - 32, 32))
                            log.LogWarning("Not Specified", "Download of '" + urlStr + "' failed. Check sum did not match");
                        #endregion

                        Case_Dto dto = sqlController.FileCaseFindMUId(urlStr);
                        File_Dto fDto = new File_Dto(dto.SiteUId, dto.CaseType, dto.CaseUId, dto.MicrotingUId, dto.CheckUId, fileLocationPicture + fileName);
                        try { HandleFileDownloaded?.Invoke(fDto, EventArgs.Empty); }
                        catch { log.LogWarning("Not Specified", "HandleFileDownloaded event's external logic suffered an Expection"); }
                        log.LogStandard("Not Specified", "Downloaded file '" + urlStr + "'.");

                        sqlController.FileProcessed(urlStr, chechSum, fileLocationPicture, fileName, ud.Id);
                    }
                    #endregion

                    updateIsRunningFiles = false;
                }
            }
            catch (ThreadAbortException)
            {
                log.LogWarning("Not Specified", t.GetMethodName() + " catch of ThreadAbortException");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", t.GetMethodName() + " failed", ex, true);
            }
        }

        private void CoreHandleUpdateNotifications()
        {
            try
            {
                if (!updateIsRunningNotifications)
                {
                    updateIsRunningNotifications = true;

                    #region update notifications
                    Note_Dto notification;
                    string noteUId;
                    bool oneFound = true;
                    while (oneFound)
                    {
                        notification = sqlController.NotificationReadFirst();
                        #region if no new notification found - stops method
                        if (notification == null)
                        {
                            oneFound = false;
                            break;
                        }
                        #endregion

                        noteUId = notification.MicrotingUId;
                        log.LogEverything("Not Specified", "Received notification:" + notification.ToString());

                        try
                        {
                            switch (notification.Activity)
                            {
                                #region check_status / checklist completed on the device
                                case "check_status":
                                    {
                                        List<Case_Dto> lstCase = new List<Case_Dto>();
                                        MainElement mainElement = new MainElement();

                                        Case_Dto concreteCase = sqlController.CaseReadByMUId(noteUId);
                                        log.LogEverything("Not Specified", concreteCase.ToString() + " has been matched");

                                        if (concreteCase.CaseUId == "" || concreteCase.CaseUId == "ReversedCase")
                                            lstCase.Add(concreteCase);
                                        else
                                            lstCase = sqlController.CaseReadByCaseUId(concreteCase.CaseUId);

                                        foreach (Case_Dto aCase in lstCase)
                                        {
                                            if (aCase.SiteUId == concreteCase.SiteUId)
                                            {
                                                #region get response's data and update DB with data
                                                string checkIdLastKnown = sqlController.CaseReadCheckIdByMUId(noteUId); //null if NOT a checkListSite
                                                log.LogVariable("Not Specified", nameof(checkIdLastKnown), checkIdLastKnown);

                                                string respXml;
                                                if (checkIdLastKnown == null)
                                                    respXml = communicator.Retrieve(noteUId, concreteCase.SiteUId);
                                                else
                                                    respXml = communicator.RetrieveFromId(noteUId, concreteCase.SiteUId, checkIdLastKnown);
                                                log.LogVariable("Not Specified", nameof(respXml), respXml);

                                                Response resp = new Response();
                                                resp = resp.XmlToClassUsingXmlDocument(respXml);
                                                //resp = resp.XmlToClass(respXml);

                                                if (resp.Type == Response.ResponseTypes.Success)
                                                {
                                                    log.LogEverything("Not Specified", "resp.Type == Response.ResponseTypes.Success (true)");
                                                    if (resp.Checks.Count > 0)
                                                    {
                                                        XmlDocument xDoc = new XmlDocument();

                                                        xDoc.LoadXml(respXml);
                                                        XmlNode checks = xDoc.DocumentElement.LastChild;
                                                        int i = 0;
                                                        foreach (Check check in resp.Checks)
                                                        {

                                                            int unitUId = sqlController.UnitRead(int.Parse(check.UnitId)).UnitUId;
                                                            log.LogVariable("Not Specified", nameof(unitUId), unitUId);
                                                            int workerUId = sqlController.WorkerRead(int.Parse(check.WorkerId)).WorkerUId;
                                                            log.LogVariable("Not Specified", nameof(workerUId), workerUId);

                                                            sqlController.ChecksCreate(resp, checks.ChildNodes[i].OuterXml.ToString(), i);

                                                            sqlController.CaseUpdateCompleted(noteUId, check.Id, DateTime.Parse(check.Date), workerUId, unitUId);
                                                            log.LogEverything("Not Specified", "sqlController.CaseUpdateCompleted(...)");

                                                            #region IF needed retract case, thereby completing the process
                                                            if (checkIdLastKnown == null)
                                                            {
                                                                string responseRetractionXml = communicator.Delete(aCase.MicrotingUId, aCase.SiteUId);
                                                                Response respRet = new Response();
                                                                respRet = respRet.XmlToClass(respXml);

                                                                if (respRet.Type == Response.ResponseTypes.Success)
                                                                {
                                                                    log.LogEverything("Not Specified", aCase.ToString() + " has been retracted");
                                                                }
                                                                else
                                                                    log.LogWarning("Not Specified", "Failed to retract eForm MicrotingUId:" + aCase.MicrotingUId + "/SideId:" + aCase.SiteUId + ". Not a critical issue, but needs to be fixed if repeated");
                                                            }
                                                            #endregion

                                                            sqlController.CaseRetract(noteUId, check.Id);
                                                            log.LogEverything("Not Specified", "sqlController.CaseRetract(...)");

                                                            Case_Dto cDto = sqlController.CaseReadByMUId(noteUId);
                                                            //InteractionCaseUpdate(cDto);
                                                            try { HandleCaseCompleted?.Invoke(cDto, EventArgs.Empty); }
                                                            catch { log.LogWarning("Not Specified", "HandleCaseCompleted event's external logic suffered an Expection"); }
                                                            log.LogStandard("Not Specified", cDto.ToString() + " has been completed");
                                                            i++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    log.LogEverything("Not Specified", "resp.Type == Response.ResponseTypes.Success (false)");
                                                    throw new Exception("Failed to retrive eForm " + noteUId + " from site " + aCase.SiteUId);
                                                }
                                                #endregion
                                            }
                                            else
                                            {
                                                //delete eForm on other tablets and update DB to "deleted"
                                                CaseDelete(aCase.MicrotingUId);
                                            }
                                        }

                                        sqlController.NotificationUpdate(notification.Id, notification.MicrotingUId, "processed");
                                        break;
                                    }
                                #endregion

                                #region unit fetch / checklist retrieve by device
                                case "unit_fetch":
                                    {
                                        sqlController.CaseUpdateRetrived(noteUId);
                                        Case_Dto cDto = sqlController.CaseReadByMUId(noteUId);
                                        //InteractionCaseUpdate(cDto);
                                        try { HandleCaseRetrived?.Invoke(cDto, EventArgs.Empty); }
                                        catch { log.LogWarning("Not Specified", "HandleCaseRetrived event's external logic suffered an Expection"); }
                                        log.LogStandard("Not Specified", cDto.ToString() + " has been retrived");

                                        sqlController.NotificationUpdate(notification.Id, notification.MicrotingUId, "processed");
                                        break;
                                    }
                                #endregion

                                #region unit_activate / tablet added
                                case "unit_activate":
                                    {
                                        try { HandleSiteActivated?.Invoke(noteUId, EventArgs.Empty); }
                                        catch { log.LogWarning("Not Specified", "HandleSiteActivated event's external logic suffered an Expection"); }
                                        log.LogStandard("Not Specified", noteUId + " has been added");
                                        try
                                        {
                                            Unit_Dto unitDto = sqlController.UnitRead(int.Parse(noteUId));
                                            sqlController.UnitUpdate(unitDto.UnitUId, unitDto.CustomerNo, 0, unitDto.SiteUId);
                                            sqlController.NotificationUpdate(notification.Id, notification.MicrotingUId, "processed");
                                        }
                                        catch
                                        {
                                            sqlController.NotificationUpdate(notification.Id, notification.MicrotingUId, "processed");
                                        }
                                        break;
                                    }
                                #endregion

                                default:
                                    throw new IndexOutOfRangeException("Notification activity '" + notification.Activity + "' is not known or mapped");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning("Not Specified", t.GetMethodName() + " failed." + t.PrintException("failed.Case:'" + notification + "' marked as 'not_found'.", ex));
                            sqlController.NotificationUpdate(notification.Id, notification.MicrotingUId, "not_found");
                            try { HandleNotificationNotFound?.Invoke(notification, EventArgs.Empty); }
                            catch { log.LogWarning("Not Specified", "HandleNotificationNotFound event's external logic suffered an Expection"); }
                        }
                    }
                    #endregion

                    updateIsRunningNotifications = false;
                }
            }
            catch (ThreadAbortException)
            {
                log.LogWarning("Not Specified", t.GetMethodName() + " catch of ThreadAbortException");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", t.GetMethodName() + " failed", ex, true);
            }
        }


        private void CoreHandleUpdateEntityItems()
        {
            try
            {
                if (!updateIsRunningEntities)
                {
                    updateIsRunningEntities = true;

                    #region update EntityItems
                    bool more = true;
                    entity_items eI;
                    while (more)
                    {
                        eI = sqlController.EntityItemSyncedRead();

                        if (eI != null)
                        {
                            log.LogEverything("Not Specified", "Received Entity:" + eI.ToString());

                            try
                            {
                                var type = sqlController.EntityGroupRead(eI.entity_group_id);

                                if (type != null)
                                {
                                    #region EntitySearch
                                    if (type.Type == "EntitySearch")
                                    {
                                        if (eI.workflow_state == "created")
                                        {
                                            string microtingUId = communicator.EntitySearchItemCreate(eI.entity_group_id.ToString(), eI.name, eI.description, eI.entity_item_uid);

                                            if (microtingUId != null)
                                            {
                                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, microtingUId, "created");
                                                continue;
                                            }
                                        }

                                        if (eI.workflow_state == "updated")
                                        {
                                            if (communicator.EntitySearchItemUpdate(eI.entity_group_id.ToString(), eI.microting_uid, eI.name, eI.description, eI.entity_item_uid))
                                            {
                                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "updated");
                                                continue;
                                            }
                                        }

                                        if (eI.workflow_state == "removed")
                                        {
                                            communicator.EntitySearchItemDelete(eI.microting_uid);

                                            sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "removed");
                                            continue;
                                        }
                                    }
                                    #endregion

                                    #region EntitySelect
                                    if (type.Type == "EntitySelect")
                                    {
                                        if (eI.workflow_state == "created")
                                        {
                                            // TODO! el.displayOrder missing and remove int.Parse(eI.description)
                                            string microtingUId = communicator.EntitySelectItemCreate(eI.entity_group_id.ToString(), eI.name, eI.display_index, eI.entity_item_uid);

                                            if (microtingUId != null)
                                            {
                                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, microtingUId, "created");
                                                continue;
                                            }
                                        }

                                        if (eI.workflow_state == "updated")
                                        {
                                            // TODO! el.displayOrder missing and remove int.Parse(eI.description)
                                            if (communicator.EntitySelectItemUpdate(eI.entity_group_id.ToString(), eI.microting_uid, eI.name, eI.display_index, eI.entity_item_uid))
                                            {
                                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "updated");
                                                continue;
                                            }
                                        }

                                        if (eI.workflow_state == "removed")
                                        {
                                            communicator.EntitySelectItemDelete(eI.microting_uid);

                                            sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "removed");
                                            continue;
                                        }
                                    }
                                    #endregion
                                }

                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "failed_to_sync");
                                log.LogWarning("Not Specified", "EntityItem entity_group_id:'" + eI.entity_group_id + "', entity_item_uid:'" + eI.entity_item_uid + "', microting:'" + eI.microting_uid + "', workflow_state:'" + eI.workflow_state + "',  failed to sync");
                            }
                            catch (Exception ex)
                            {
                                log.LogWarning("Not Specified", "EntityItem entity_group_id:'" + eI.entity_group_id + "', entity_item_uid:'" + eI.entity_item_uid + "', microting:'" + eI.microting_uid + "', workflow_state:'" + eI.workflow_state + "',  failed to sync. Exception:'" + ex.Message + "'");
                                sqlController.EntityItemSyncedProcessed(eI.entity_group_id, eI.entity_item_uid, eI.microting_uid, "failed to sync");
                            }
                        }
                        else
                            more = false;
                    }
                    #endregion

                    updateIsRunningEntities = false;
                }
            }
            catch (ThreadAbortException)
            {
                log.LogWarning("Not Specified", t.GetMethodName() + " catch of ThreadAbortException");
            }
            catch (Exception ex)
            {
                log.LogException("Not Specified", "CoreHandleUpdateEntityItems failed", ex, true);
            }
        }
        #endregion
    }
}