﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace eFormCustom
{
    public class SqlControllerCustom
    {
        #region var
        object _lockQuery = new object();
        string connectionStr;
        #endregion

        #region con
        public SqlControllerCustom(string connectionString)
        {
            connectionStr = connectionString;
        }
        #endregion

        #region public
        public List<int>    SitesRead(string type)
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<int> rtnLst = new List<int>();
                    List<sites> conLst = db.sites.Where(x => x.type == type).ToList();

                    if (conLst.Count == 0)
                        throw new Exception("SitesRead failed, count == 0");

                    foreach (sites con in conLst)
                        rtnLst.Add(con.site_id);

                    return rtnLst;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("SitesRead failed. Type:" + type, ex);
            }
        }

        public List<string> ContainerRead()
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<string> rtnLst = new List<string>();
                    List<containers> conLst = db.containers.ToList();

                    if (conLst.Count == 0)
                        throw new Exception("ContainerRead failed, count == 0");

                    foreach (containers con in conLst)
                        rtnLst.Add(con.name);

                    return rtnLst;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ContainerRead failed.", ex);
            }
        }

        public List<string> FactionRead()
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<string> rtnLst = new List<string>();
                    List<factions> conLst = db.factions.ToList();

                    if (conLst.Count == 0)
                        throw new Exception("FactionRead failed, count == 0");

                    foreach (factions con in conLst)
                        rtnLst.Add(con.name);

                    return rtnLst;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("FactionRead failed.", ex);
            }
        }

        public List<string> LocationRead()
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<string> rtnLst = new List<string>();
                    List<locations> conLst = db.locations.ToList();

                    if (conLst.Count == 0)
                        throw new Exception("LocationRead failed, count == 0");

                    foreach (locations con in conLst)
                        rtnLst.Add(con.name);

                    return rtnLst;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("LocationRead failed.", ex);
            }
        }
        
        public List<string> WorkerRead(int siteId)
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<string> rtnLst = new List<string>();
                    List<workers> conLst = db.workers.Where(x => x.site_id == siteId).ToList();

                    if (conLst.Count != 1)
                        throw new Exception("WorkerRead failed, count != 1");

                    rtnLst.Add(conLst[0].location);
                    rtnLst.Add(conLst[0].name);
                    rtnLst.Add(conLst[0].phone);

                    return rtnLst;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("WorkerRead failed.", ex);
            }
        }

        public int          TemplatIdRead(string name)
        {
            try
            {
                using (var db = new CustomDb(connectionStr))
                {
                    List<string> rtnLst = new List<string>();
                    List<templat_ids> conLst = db.templat_ids.Where(x => x.name == name).ToList();

                    if (conLst.Count != 1)
                        throw new Exception("TemplatIdRead failed, count != 1");

                    return conLst[0].templat_Id;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("TemplatIdRead failed.", ex);
            }
        }
        #endregion

        #region Get*
        private bool Bool(short? input)
        {
            if (input == null)
                return false;
            string temp = input.ToString();
            if (temp == "0" || temp.ToLower() == "false" || temp == "")
                return false;
            if (temp == "1" || temp.ToLower() == "true")
                return true;
            throw new Exception(temp + ": was not found to be a bool");
        }

        private bool Bool(string input)
        {
            if (input == null)
                return false;
            if (input == "0" || input.ToLower() == "false" || input == "")
                return false;
            if (input == "1" || input.ToLower() == "true")
                return true;
            throw new Exception(input + ": was not found to be a bool");
        }

        private short Bool(bool inputBool)
        {
            if (inputBool == false)
                return 0;
            else
                return 1;
        }

        private int Int(int? input)
        {
            if (input == null)
                return 0;

            string str = input.ToString();
            return int.Parse(str);
        }

        private DateTime? Date(string input)
        {
            if (input == "")
                return null;
            else
                return DateTime.Parse(input);
        }

        private DateTime Date(DateTime? input)
        {
            if (input != null)
                return (DateTime)input;
            else
                return DateTime.MinValue;
        }
        #endregion
    }
}