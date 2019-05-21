﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Text;
using Testflow.Modules;
using Testflow.Runtime.Data;
using Testflow.Usr;
using Testflow.Utility.I18nUtil;

namespace Testflow.DataMaintainer
{
    internal abstract class DatabaseProxy
    {
        public bool IsRuntimeModule { get; }

        protected readonly ILogService Logger;
        protected DbConnection Connection;
        protected readonly I18N I18N;

        protected readonly IModuleConfigData ConfigData;
        protected DataModelMapper DataModelMapper;

        protected DatabaseProxy(IModuleConfigData configData, bool isRuntimeModuleModule)
        {
            this.ConfigData = configData;
            IsRuntimeModule = isRuntimeModuleModule;

            I18NOption i18NOption = new I18NOption(this.GetType().Assembly, "i18n_datamaintain_zh", "i18n_datamaintain_en")
            {
                Name = Constants.I18nName
            };
            I18N.InitInstance(i18NOption);
            I18N = I18N.GetInstance(Constants.I18nName);
            Logger = TestflowRunner.GetInstance().LogService;
            try
            {
                // 使用DbProviderFactory方式连接需要在App.Config文件中定义DbProviderFactories节点
                // 但是App.Config文件只在入口Assembly中时才会被默认加载，所以目前写死为SqlConnection
//                DbProviderFactory factory = DbProviderFactories.GetFactory("System.Data.SQLite");
//                Connection = factory.CreateConnection();
//                if (null == Connection)
//                {
//                    Logger.Print(LogLevel.Fatal, CommonConst.PlatformLogSession, "Connect db failed.");
//                    throw new TestflowRuntimeException(ModuleErrorCode.ConnectDbFailed, I18N.GetStr("ConnectDbFailed"));
//                }
                InitializeDatabaseAndConnection();
            }
            catch (DbException ex)
            {
                Logger.Print(LogLevel.Fatal, CommonConst.PlatformLogSession, ex, "Connect db failed.");
                throw new TestflowRuntimeException(ModuleErrorCode.ConnectDbFailed, I18N.GetStr("ConnectDbFailed"), ex);
            }

            DataModelMapper = new DataModelMapper();
        }

        public virtual int GetTestInstanceCount(string filterString)
        {
            string cmd = SqlCommandFactory.CreateCalcCountCmd(filterString, DataBaseItemNames.InstanceTableName);
            using (DbDataReader dataReader = ExecuteReadCommand(cmd))
            {
                int count = 0;
                if (dataReader.Read())
                {
                    count = dataReader.GetInt32(0);
                }
                return count;
            }
        }

        public virtual TestInstanceData GetTestInstanceData(string runtimeHash)
        {
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{runtimeHash}'";
            string cmd = SqlCommandFactory.CreateQueryCmd(filter, DataBaseItemNames.InstanceTableName);
            DbDataReader dataReader = ExecuteReadCommand(cmd);
            TestInstanceData instanceData;
            if (dataReader.Read())
            {
                instanceData = new TestInstanceData();
                DataModelMapper.ReadToObject(dataReader, instanceData);
            }
            else
            {
                instanceData = null;
            }
            return instanceData;
        }

        public virtual IList<TestInstanceData> GetTestInstanceDatas(string filterString)
        {
            string cmd = SqlCommandFactory.CreateQueryCmd(filterString, DataBaseItemNames.InstanceTableName);
            DbDataReader dataReader = ExecuteReadCommand(cmd);
            List<TestInstanceData> testInstanceDatas = new List<TestInstanceData>(50);
            while (dataReader.Read())
            {
                TestInstanceData instanceData = new TestInstanceData();
                DataModelMapper.ReadToObject(dataReader, instanceData);
                testInstanceDatas.Add(instanceData);
            }
            return testInstanceDatas;
        }

        public virtual void AddData(TestInstanceData testInstance)
        {
            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(testInstance);
            string cmd = SqlCommandFactory.CreateInsertCmd(DataBaseItemNames.InstanceTableName, columnToValue);
            ExecuteWriteCommand(cmd);
        }

        public virtual void UpdateData(TestInstanceData testInstance)
        {
            // 获取原数据，转换为键值对类型
            TestInstanceData lastInstanceData = GetTestInstanceData(testInstance.RuntimeHash);
            Dictionary<string, string> lastInstanceValues = DataModelMapper.GetColumnValueMapping(lastInstanceData);

            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(testInstance);
            // 比较并创建更新命令
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{testInstance.RuntimeHash}'";
            string cmd = SqlCommandFactory.CreateUpdateCmd(DataBaseItemNames.InstanceTableName, lastInstanceValues,
                columnToValue, filter);
            ExecuteWriteCommand(cmd);
        }

        public virtual void DeleteTestInstance(string fileterString)
        {
            DbTransaction transaction = null;
            try
            {
                // 删除TestInstance需要执行事务流程
                transaction = Connection.BeginTransaction(IsolationLevel.Serializable);

                string deleteCmd = SqlCommandFactory.CreateDeleteCmd(DataBaseItemNames.StatusTableName, fileterString);
                ExecuteWriteCommand(deleteCmd, transaction);

                deleteCmd = SqlCommandFactory.CreateDeleteCmd(DataBaseItemNames.PerformanceTableName, fileterString);
                ExecuteWriteCommand(deleteCmd, transaction);

                deleteCmd = SqlCommandFactory.CreateDeleteCmd(DataBaseItemNames.SequenceTableName, fileterString);
                ExecuteWriteCommand(deleteCmd, transaction);

                deleteCmd = SqlCommandFactory.CreateDeleteCmd(DataBaseItemNames.SessionTableName, fileterString);
                ExecuteWriteCommand(deleteCmd, transaction);

                deleteCmd = SqlCommandFactory.CreateDeleteCmd(DataBaseItemNames.InstanceTableName, fileterString);
                ExecuteWriteCommand(deleteCmd, transaction);

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        public virtual IList<SessionResultData> GetSessionResults(string runtimeHash)
        {
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{runtimeHash}'";
            List<SessionResultData> resultDatas = new List<SessionResultData>(10);

            string cmd = SqlCommandFactory.CreateQueryCmd(filter, DataBaseItemNames.SessionTableName);

            DbDataReader dataReader = ExecuteReadCommand(cmd);
            while (dataReader.Read())
            {
                SessionResultData sessionResultData = new SessionResultData();
                DataModelMapper.ReadToObject(dataReader, sessionResultData);
                resultDatas.Add(sessionResultData);
            }

            return resultDatas;
        }

        public virtual SessionResultData GetSessionResult(string runtimeHash, int sessionId)
        {
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{runtimeHash}' AND {DataBaseItemNames.SessionIdColumn}={sessionId}";
            string cmd = SqlCommandFactory.CreateQueryCmd(filter, DataBaseItemNames.SessionTableName);

            DbDataReader dataReader = ExecuteReadCommand(cmd);

            SessionResultData sessionResultData = null;
            if (dataReader.Read())
            {
                sessionResultData = new SessionResultData();
                DataModelMapper.ReadToObject(dataReader, sessionResultData);
            }
            return sessionResultData;
        }

        public virtual void AddData(SessionResultData sessionResult)
        {
            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(sessionResult);
            string cmd = SqlCommandFactory.CreateInsertCmd(DataBaseItemNames.SessionTableName, columnToValue);
            ExecuteWriteCommand(cmd);
        }

        public virtual void UpdateData(SessionResultData sessionResult)
        {
            SessionResultData lastSessionResult = GetSessionResult(sessionResult.RuntimeHash, sessionResult.Session);
            Dictionary<string, string> lastSessionValues = DataModelMapper.GetColumnValueMapping(lastSessionResult);

            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(sessionResult);

            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{sessionResult.RuntimeHash}' AND {DataBaseItemNames.SessionIdColumn}={sessionResult.Session}";
            string cmd = SqlCommandFactory.CreateUpdateCmd(DataBaseItemNames.SessionTableName, lastSessionValues,
                columnToValue, filter);
            ExecuteWriteCommand(cmd);
        }

        public virtual IList<SequenceResultData> GetSequenceResultDatas(string runtimeHash, int sessionId)
        {
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{runtimeHash}' AND {DataBaseItemNames.SessionIdColumn}={sessionId}";
            string cmd = SqlCommandFactory.CreateQueryCmd(filter, DataBaseItemNames.SequenceTableName);

            DbDataReader dataReader = ExecuteReadCommand(cmd);
            List<SequenceResultData> resultDatas = new List<SequenceResultData>(10);
            while (dataReader.Read())
            {
                SequenceResultData sequenceResultData = new SequenceResultData();
                DataModelMapper.ReadToObject(dataReader, sequenceResultData);
                resultDatas.Add(sequenceResultData);
            }
            return resultDatas;
        }

        public virtual SequenceResultData GetSequenceResultData(string runtimeHash, int sessionId, int sequenceIndex)
        {
            string filter = $"{DataBaseItemNames.RuntimeIdColumn}='{runtimeHash}' AND {DataBaseItemNames.SessionIdColumn}={sessionId} AND " +
                            $"{DataBaseItemNames.SequenceIndexColumn}={sequenceIndex}";
            string cmd = SqlCommandFactory.CreateQueryCmd(filter, DataBaseItemNames.SequenceTableName);

            DbDataReader dataReader = ExecuteReadCommand(cmd);
            SequenceResultData sequenceResultData = null;
            if (dataReader.Read())
            {
                sequenceResultData = new SequenceResultData();
                DataModelMapper.ReadToObject(dataReader, sequenceResultData);
            }
            return sequenceResultData;
        }

        public virtual void AddData(SequenceResultData sequenceResult)
        {
            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(sequenceResult);
            string cmd = SqlCommandFactory.CreateInsertCmd(DataBaseItemNames.SequenceTableName, columnToValue);
            ExecuteWriteCommand(cmd);
        }

        public virtual void UpdateData(SequenceResultData sequenceResult)
        {
            SequenceResultData lastSequenceResult = GetSequenceResultData(sequenceResult.RuntimeHash,
                sequenceResult.Session, sequenceResult.SequenceIndex);


            Dictionary<string, string> lastSequenceValues = DataModelMapper.GetColumnValueMapping(lastSequenceResult);

            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(sequenceResult);

            string filter =
                $"{DataBaseItemNames.RuntimeIdColumn}='{sequenceResult.RuntimeHash}' AND {DataBaseItemNames.SessionIdColumn}={sequenceResult.Session} AND {DataBaseItemNames.SequenceIndexColumn}={sequenceResult.SequenceIndex}";

            string cmd = SqlCommandFactory.CreateUpdateCmd(DataBaseItemNames.SequenceTableName, lastSequenceValues,
                columnToValue, filter);
            ExecuteWriteCommand(cmd);
        }

        public virtual void AddData(PerformanceStatus performanceStatus)
        {
            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(performanceStatus);
            string cmd = SqlCommandFactory.CreateInsertCmd(DataBaseItemNames.PerformanceTableName, columnToValue);
            ExecuteWriteCommand(cmd);
        }

        public virtual void AddData(RuntimeStatusData runtimeStatus)
        {
            Dictionary<string, string> columnToValue = DataModelMapper.GetColumnValueMapping(runtimeStatus);
            string cmd = SqlCommandFactory.CreateInsertCmd(DataBaseItemNames.StatusTableName, columnToValue);
            ExecuteWriteCommand(cmd);
        }

        protected DbDataReader ExecuteReadCommand(string command, DbTransaction transaction = null)
        {
            try
            {
                DbCommand dbCommand = Connection.CreateCommand();
                dbCommand.CommandText = command;
                dbCommand.CommandTimeout = Constants.CommandTimeout;
                if (null != transaction)
                {
                    dbCommand.Transaction = transaction;
                }
                return dbCommand.ExecuteReader();
            }
            catch (DbException ex)
            {
                Logger.Print(LogLevel.Fatal, CommonConst.PlatformLogSession, ex, "Database operation failed.");
                throw new TestflowRuntimeException(ModuleErrorCode.DbOperationFailed, I18N.GetStr("DbOperationFailed"), ex);
            }
        }

        protected void ExecuteWriteCommand(string command, DbTransaction transaction = null)
        {
            try
            {
                DbCommand dbCommand = Connection.CreateCommand();
                dbCommand.CommandText = command;
                dbCommand.CommandTimeout = Constants.CommandTimeout;
                if (null != transaction)
                {
                    dbCommand.Transaction = transaction;
                }
                dbCommand.ExecuteNonQuery();
            }
            catch (DbException ex)
            {
                Logger.Print(LogLevel.Fatal, CommonConst.PlatformLogSession, ex, "Database operation failed.");
                throw new TestflowRuntimeException(ModuleErrorCode.DbOperationFailed, I18N.GetStr("DbOperationFailed"), ex);
            }
        }

        private void InitializeDatabaseAndConnection()
        {
            string testflowHome = Environment.GetEnvironmentVariable(CommonConst.EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(testflowHome))
            {
                testflowHome = Environment.CurrentDirectory;
            }
            if (testflowHome.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                testflowHome += Path.DirectorySeparatorChar;
            }
            string databaseFilePath =
                $"{testflowHome}{CommonConst.DataDir}{Path.DirectorySeparatorChar}{Constants.DataBaseName}";
            // 使用DbProviderFactory方式连接需要在App.Config文件中定义DbProviderFactories节点
            // 但是App.Config文件只在入口Assembly中时才会被默认加载，所以目前写死为SqlConnection
//            Connection.ConnectionString = $"Data Source={databaseFilePath}";

            // 如果已经存在则直接跳出
            if (File.Exists(databaseFilePath))
            {
                Connection = new SQLiteConnection($"Data Source={databaseFilePath}");
                Connection.Open();
                return;
            }
            string databaseFileDir = $"{testflowHome}{CommonConst.DataDir}";
            if (!Directory.Exists(databaseFileDir))
            {
                Directory.CreateDirectory(CommonConst.DataDir);
            }
            Connection = new SQLiteConnection($"Data Source={databaseFilePath}");
            DbTransaction transaction = null;
            try
            {
                const string endDelim = ";";
                const string commentPrefix = "--";
                Connection.Open();
                string sqlFilePath =
                    $"{testflowHome}{CommonConst.DeployDir}{Path.DirectorySeparatorChar}{Constants.SqlFileName}";
                using (StreamReader reader = new StreamReader(sqlFilePath, Encoding.UTF8))
                {
                    StringBuilder createTableCmd = new StringBuilder(500);
                    string lineData;
                    transaction = Connection.BeginTransaction(IsolationLevel.Serializable);
                    while (null != (lineData = reader.ReadLine()))
                    {
                        lineData = lineData.Trim();
                        if (lineData.StartsWith(commentPrefix))
                        {
                            continue;
                        }
                        createTableCmd.Append(lineData);
                        if (lineData.EndsWith(endDelim))
                        {
                            DbCommand dbCommand = Connection.CreateCommand();
                            dbCommand.CommandText = createTableCmd.ToString();
                            dbCommand.Transaction = transaction;
                            dbCommand.ExecuteNonQuery();
                            createTableCmd.Clear();
                        }
                    }
                    transaction.Commit();
                }
            }
            catch (Exception)
            {
                transaction?.Rollback();
                transaction?.Dispose();
                Connection?.Dispose();
                // 如果失败则删除文件
                File.Delete(databaseFilePath);
                throw;
            }
        }

        public void Dispose()
        {
            Connection?.Close();
        }
    }
}