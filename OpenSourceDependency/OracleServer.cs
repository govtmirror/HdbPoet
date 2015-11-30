using System;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using System.Collections;
using System.Windows.Forms;

namespace HdbPoet
{
    /// <summary>
    /// Oracle Database specific
    /// </summary>
    public class OracleServer: Reclamation.Core.BasicDBServer
    {
        string strAccessConn = null;
        string lastSqlCommand;
        public ArrayList sqlCommands = new ArrayList();
        string lastMessage;
        string username, service = "";

        public string Username
        {
            get { return username; }
            set { username = value; }
        }
        string host = "";

        public string Host
        {
            get { return host; }
            set { host = value; }
        }
        bool loginCanceled = false;
        string m_timeZone = "";

        public string TimeZone
        {
            get { return m_timeZone; }
            //set { m_timeZone = value; }
        }
        string m_port = "";
        public string Port
        {
            get { return m_port; }
            set { m_port = value; }
        }

        public override string DataSource
        {
            get { return Host + ":" + service; }
        }
        /// <summary>
        /// Creates instance of Oracle class with inputs.
        /// </summary>
        public OracleServer(string username, string password, string host, string service, string timeZone, string port)
        {
            this.username = username;
            this.service = service;
            this.host = host;
            this.m_timeZone = timeZone;
            this.m_port = port;
            sqlCommands.Clear();
            MakeConnectionString(username, password);
        }


        public string[] OracleUsers
        {
            get
            {

                var tbl = Table("a", "select username from all_users order by username");

                return (from row in tbl.AsEnumerable()
                        select row.Field<string>("username")).ToArray();
            }
        }


        public bool LoginCanceled
        {
            get { return loginCanceled; }
        }

        public string ServiceName
        {
            get { return this.service; }
        }

        public string ConnectionString
        {
            get { return this.strAccessConn; }
        }


        /// <summary>
        /// returns true if connection is working.
        /// </summary>
        /// <returns></returns>
        public bool ConnectionWorking()
        {
            string sql = "select count(*) from hdb_site";
            DataTable tbl = this.Table("test", sql, true);
            return true;
        }


        void MakeConnectionString(string username, string password)
        {
            //strAccessConn = "Provider="+provider+";User ID="+username+";"
            //    +"Password="+password+"; Data Source="+service+";";
            strAccessConn = "Data Source=(DESCRIPTION="
             + "(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=" + host + ")(PORT=1521)))"
             + "(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=" + service + ")));"
             + "User Id=" + username + ";Password=" + password + ";";
        }


        public override DataTable Table(string tableName, string sql)
        {
            return Table(tableName, sql, true);
        }
        public override DataTable Table(string tableName)
        {
            return Table(tableName, "select * from " + tableName);
        }

        DataTable Table(string tableName, string sql, bool throwErrors)
        {
            string strAccessSelect = sql;
            OracleConnection myAccessConn = new OracleConnection(strAccessConn);
            OracleCommand myAccessCommand = new OracleCommand(strAccessSelect, myAccessConn);
            OracleDataAdapter myDataAdapter = new OracleDataAdapter(myAccessCommand);

            //Console.WriteLine(sql);
            this.lastSqlCommand = sql;
            this.sqlCommands.Add(sql);
            DataSet myDataSet = new DataSet();
            try
            {
                myAccessConn.Open();

                myDataAdapter.Fill(myDataSet, tableName);
            }
            catch (Exception e)
            {
                string msg = "Error reading from database \n" + sql + "\n Exception " + e.ToString();
                Console.WriteLine(msg);

                if (throwErrors)
                {
                    throw e;
                }

                System.Windows.Forms.MessageBox.Show(msg, "Error",
                  System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                //throw e; 
            }
            finally
            {
                myAccessConn.Close();
            }
            DataTable tbl = myDataSet.Tables[tableName];
            return tbl;
        }


        public override int RunSqlCommand(string sql)
        {
            return this.RunSqlCommand(sql, this.strAccessConn);
        }


        public int RunStoredProc(OracleCommand cmd)
        {
            int rval = 0;
            OracleConnection conn = new OracleConnection(this.strAccessConn);
            Debug.Assert(cmd.CommandType == CommandType.StoredProcedure);
            cmd.Connection = conn;

            rval = -1;
            try
            {
                conn.Open();
                rval = cmd.ExecuteNonQuery();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
                throw exc;
                //rval = -1;
            }

            conn.Close();


            string msg = cmd.CommandText + " ";
            for (int i = 0; i < cmd.Parameters.Count; i++)
            {
                msg += "," + cmd.Parameters[i].Value.ToString();

            }
            this.lastSqlCommand = msg;
            this.sqlCommands.Add(msg);
            return rval;
        }
        /// <summary>
        /// runs sql command.
        /// returns number of rows affected.
        /// </summary>
        /// <returns></returns>
        public int RunSqlCommand(string sql, string strAccessConn)
        {
            int rval = 0;
            this.lastMessage = "";
            OracleConnection myConnection = new OracleConnection(strAccessConn);
            myConnection.Open();
            OracleCommand myCommand = new OracleCommand();
            OracleTransaction myTrans;

            // Start a local transaction
            myTrans = myConnection.BeginTransaction(IsolationLevel.ReadCommitted);
            // Assign transaction object for a pending local transaction
            myCommand.Connection = myConnection;
            myCommand.Transaction = myTrans;

            try
            {
                myCommand.CommandText = sql;
                rval = myCommand.ExecuteNonQuery();
                myTrans.Commit();
                this.lastSqlCommand = sql;
                this.sqlCommands.Add(sql);
            }
            catch (Exception e)
            {
                myTrans.Rollback();
                Console.WriteLine(e.ToString());
                System.Windows.Forms.MessageBox.Show("Error running command :" + sql + " exception: " + e.ToString());
                Console.WriteLine("Error running " + sql);
                this.lastMessage = e.ToString();
                rval = -1;
                //throw e;
            }
            finally
            {
                myConnection.Close();
            }
            return rval;
        }

        public string[] SqlHistory
        {
            get
            {
                string[] rval = new String[sqlCommands.Count];
                this.sqlCommands.CopyTo(rval);
                return rval;
            }
            set
            {
                this.sqlCommands.Clear();
                for (int i = 0; i < value.Length; i++)
                {
                    this.sqlCommands.Add(value[i]);
                }
            }

        }
        public string LastSqlCommand
        {
            get { return this.lastSqlCommand; }
        }


        public static OracleServer ConnectToOracle(string hostname="")
        {
            DialogResult dr;
            bool loginSucess = false;
            OracleLogin login = new OracleLogin(hostname);

            OracleServer oracle = null;
            do
            {
                dr = login.ShowDialog();
                if (dr == DialogResult.Cancel)
                    return null;


                oracle = new OracleServer(login.ConnectionInfo.Username,
                    login.ConnectionInfo.Password,
                    login.ConnectionInfo.Host, 
                    login.ConnectionInfo.Service,
                    login.ConnectionInfo.Timezone, "1521");
                try
                {
                    loginSucess = oracle.ConnectionWorking();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source);
                    if (ex.InnerException != null)
                        MessageBox.Show(ex.InnerException.Message);
                }

            } while (!loginSucess);

            if (oracle.LoginCanceled)
            {
                return null;
            }
            return oracle;
        }


        //Saves DataTable in database
        public override int SaveTable(DataTable dataTable)
        {
            string sql = "select * from " + dataTable.TableName + " where 2 = 1";
            return SaveTable(dataTable, sql);

        }

        public override int SaveTable(DataTable dataTable, string sql)
        {
            Console.WriteLine("Saving " + dataTable.TableName);
            DataSet myDataSet = new DataSet();
            myDataSet.Tables.Add(dataTable.TableName);

            OracleConnection myAccessConn = new OracleConnection(ConnectionString);
            OracleCommand myAccessCommand = new OracleCommand(sql, myAccessConn);
            OracleDataAdapter myDataAdapter = new OracleDataAdapter(myAccessCommand);
            OracleCommandBuilder cb = new OracleCommandBuilder(myDataAdapter);
            //            myDataAdapter.InsertCommand =  cb.GetInsertCommand();
            this.lastSqlCommand = sql;
            SqlCommands.Add(sql);

            myAccessConn.Open();
            int recordCount = 0;
            try
            {   // call Fill method only to make things work. (we ignore myDataSet)
                myDataAdapter.Fill(myDataSet, dataTable.TableName);
                recordCount = myDataAdapter.Update(dataTable);

            }
            finally
            {
                myAccessConn.Close();
            }
            return recordCount;
        }


    }
}