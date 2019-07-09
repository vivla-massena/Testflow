﻿using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Testflow.Usr;
using Testflow.CoreCommon;
using Testflow.CoreCommon.Common;
using Testflow.Data;
using Testflow.Data.Sequence;
using Testflow.Runtime;
using Testflow.Runtime.Data;
using Testflow.SlaveCore.Common;
using Testflow.SlaveCore.Data;

namespace Testflow.SlaveCore.Runner.Model
{
    internal class StepExecutionEntity : StepTaskEntityBase
    {
        #region 序列功能标志

        public bool HasLoopCount { get; }

        public bool HasRetryCount { get; }

        public FunctionType FunctionType { get; }

        #endregion

        #region 子序列属性

        public StepTaskEntityBase SubStepRoot { get; }

        #endregion


        #region 方法属性

        public Type ClassType { get; set; }

        public MethodInfo Method { get; set; }

        public ConstructorInfo Constructor { get; set; }

        public object[] Params { get; }

        public string InstanceVar { get; set; }

        public string ReturnVar { get; set; }

        #endregion


        #region 计数相关

        public int LoopCount { get; set; }

        public int MaxLoopCount { get; }

        public string LoopVar { get; }

        public int RetryCount { get; set; }

        public int MaxRetryCount { get; }

        public string RetryVar { get; }

        #endregion

        public StepExecutionEntity(ISequenceStep step, SlaveContext context, int sequenceIndex) : base(step, context, sequenceIndex)
        {
            this.HasLoopCount = false;
            this.HasRetryCount = false;

            int session = context.SessionId;
            this.LoopCount = 0;
            this.RetryCount = 0;

            if (StepData.HasSubSteps)
            {
                this.SubStepRoot = ModuleUtils.CreateSubStepModelChain(StepData.SubSteps, Context, sequenceIndex);
            }
            else
            {
                this.Method = null;
                this.Params = new object[step.Function.Parameters?.Count ?? 0];
                this.Constructor = null;
                this.FunctionType = step.Function.Type;

                if (CoreUtils.IsValidVaraible(step.Function.Instance))
                {
                    string variableName = ModuleUtils.GetVariableNameFromParamValue(step.Function.Instance);
                    this.InstanceVar = ModuleUtils.GetVariableFullName(variableName, step, session);
                }
                if (CoreUtils.IsValidVaraible(step.Function.Return))
                {
                    string variableName = ModuleUtils.GetVariableNameFromParamValue(step.Function.Return);
                    this.ReturnVar = ModuleUtils.GetVariableFullName(variableName, step, session);
                }
            }

            if (null != step.LoopCounter && step.LoopCounter.MaxValue > 1 && step.LoopCounter.CounterEnabled)
            {
                this.HasLoopCount = true;
                this.MaxLoopCount = step.LoopCounter.MaxValue;
                if (CoreUtils.IsValidVaraible(step.LoopCounter.CounterVariable))
                {
                    string variable = ModuleUtils.GetVariableNameFromParamValue(step.LoopCounter.CounterVariable);
                    this.LoopVar = ModuleUtils.GetVariableFullName(variable, step, session);
                }
            }

            if (null != step.RetryCounter && step.RetryCounter.MaxRetryTimes > 1 && step.RetryCounter.RetryEnabled)
            {
                this.HasRetryCount = true;
                this.MaxRetryCount = step.RetryCounter.MaxRetryTimes;
                if (CoreUtils.IsValidVaraible(step.RetryCounter.CounterVariable))
                {
                    string variable = ModuleUtils.GetVariableNameFromParamValue(step.RetryCounter.CounterVariable);
                    this.RetryVar = ModuleUtils.GetVariableFullName(variable, step, session);
                }
            }
        }
        
        public override void GenerateInvokeInfo()
        {
            if (StepData.HasSubSteps)
            {
                this.SubStepRoot.GenerateInvokeInfo();
            }
            else
            {
                switch (FunctionType)
                {
                    case FunctionType.StaticFunction:
                    case FunctionType.InstanceFunction:
                        this.Method = Context.TypeInvoker.GetMethod(StepData.Function);
                        if (null == Method)
                        {
                            throw new TestflowRuntimeException(ModuleErrorCode.RuntimeError,
                                Context.I18N.GetFStr("LoadFunctionFailed", StepData.Function.MethodName));
                        }
                        break;
                    case FunctionType.Constructor:
                        this.Constructor = Context.TypeInvoker.GetConstructor(StepData.Function);
                        if (null == Constructor)
                        {
                            throw new TestflowRuntimeException(ModuleErrorCode.RuntimeError,
                                Context.I18N.GetFStr("LoadFunctionFailed", StepData.Function.MethodName));
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            NextStep?.GenerateInvokeInfo();
        }

        public override void InitializeParamsValues()
        {
            if (StepData.HasSubSteps)
            {
                SubStepRoot.InitializeParamsValues();
            }
            else
            {
                IArgumentCollection argumentInfos = StepData.Function.ParameterType;
                IParameterDataCollection parameters = StepData.Function.Parameters;
                for (int i = 0; i < argumentInfos.Count; i++)
                {
                    string paramValue = parameters[i].Value;
                    if (parameters[i].ParameterType == ParameterType.Value)
                    {
                        Params[i] = Context.TypeInvoker.CastValue(argumentInfos[i].Type, paramValue);
                    }
                    else
                    {
                        // 如果是变量，则先获取对应的Varaible变量，真正的值在运行时才更新获取
                        string variableName = ModuleUtils.GetVariableNameFromParamValue(paramValue);
                        IVariable variable = ModuleUtils.GetVaraibleByRawVarName(variableName, StepData);
                        if (null == variable)
                        {
                            Context.LogSession.Print(LogLevel.Error, SequenceIndex,
                                $"Unexist variable '{variableName}' in sequence data.");
                            throw new TestflowDataException(ModuleErrorCode.SequenceDataError,
                                Context.I18N.GetFStr("UnexistVariable", variableName));

                        }
                        // 将变量的值保存到Parameter中
                        string varFullName = CoreUtils.GetRuntimeVariableName(Context.SessionId, variable);
                        parameters[i].Value = ModuleUtils.GetFullParameterVariableName(varFullName, parameters[i].Value);
                        Params[i] = null;
                    }
                }
                if (null != StepData.Function.ReturnType && CoreUtils.IsValidVaraible(StepData.Function.Return))
                {
                    // 如果是变量，则先获取对应的Varaible变量，真正的值在运行时才更新获取
                    string variableName = ModuleUtils.GetVariableNameFromParamValue(StepData.Function.Return);
                    IVariable variable = ModuleUtils.GetVaraibleByRawVarName(variableName, StepData);
                    if (null == variable)
                    {
                        Context.LogSession.Print(LogLevel.Error, SequenceIndex,
                            $"Unexist variable '{variableName}' in sequence data.");
                        throw new TestflowDataException(ModuleErrorCode.SequenceDataError,
                            Context.I18N.GetFStr("UnexistVariable", variableName));
                    }
                    ReturnVar = CoreUtils.GetRuntimeVariableName(Context.SessionId, variable);
                }
            }
            NextStep?.InitializeParamsValues();
        }

        protected override void InvokeStep()
        {
            this.Result = StepResult.Error;
            switch (StepData.Behavior)
            {
                case RunBehavior.Normal:
                    ExecuteSequenceStep();
                    this.Result = StepResult.Pass;
                    break;
                case RunBehavior.Skip:
                    this.Result = StepResult.Skip;
                    break;
                case RunBehavior.ForceSuccess:
                    try
                    {
                        ExecuteSequenceStep();
                        this.Result = StepResult.Pass;
                    }
                    catch (TaskFailedException ex)
                    {
                        this.Result = StepResult.Failed;
                        Context.LogSession.Print(LogLevel.Warn, SequenceIndex, ex,
                            "Execute failed but force success.");
                    }
                    break;
                case RunBehavior.ForceFailed:
                    ExecuteSequenceStep();
                    this.Result = StepResult.Failed;
                    // 抛出强制失败异常
                    throw new TaskFailedException(SequenceIndex, FailedType.ForceFailed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            // 如果当前step被标记为记录状态，则返回状态信息
            if (StepData.RecordStatus)
            {
                SequenceStatusInfo statusInfo = new SequenceStatusInfo(SequenceIndex, this.GetStack(),
                    StatusReportType.Record, Result);
                // 更新watch变量值
                statusInfo.WatchDatas = Context.VariableMapper.GetWatchDataValues(StepData);
                Context.StatusQueue.Enqueue(statusInfo);
            }
        }

        private void ExecuteSequenceStep()
        {
            if (!HasLoopCount)
            {
                ExecuteStepSingleTime();
            }
            else
            {
                LoopCount = 0;
                do
                {
                    if (CoreUtils.IsValidVaraible(StepData.LoopCounter.CounterVariable))
                    {
                        Context.VariableMapper.SetParamValue(LoopVar, StepData.LoopCounter.CounterVariable, LoopCount,
                            false);
                    }
                    ExecuteStepSingleTime();
                    if (CoreUtils.IsValidVaraible(LoopVar))
                    {
                        Context.VariableMapper.SetParamValue(LoopVar, StepData.LoopCounter.CounterVariable, LoopCount,
                            StepData.RecordStatus);
                    }
                } while (++LoopCount < MaxLoopCount);
            }
        }

        private void ExecuteStepSingleTime()
        {
            if (StepData.HasSubSteps)
            {
                SubStepRoot.Invoke();
            }
            else
            {
                object instance;
                object returnValue;
                SetVariableParamValue();
                switch (FunctionType)
                {
                    case FunctionType.Constructor:
                        instance = Constructor.Invoke(Params);
                        if (CoreUtils.IsValidVaraible(InstanceVar))
                        {
                            Context.VariableMapper.SetParamValue(InstanceVar, StepData.Function.Instance, instance,
                                StepData.RecordStatus);
                            LogTraceVariable(StepData.Function.Instance, InstanceVar);
                        }
                        break;
                    case FunctionType.InstanceFunction:
                        instance = Context.VariableMapper.GetParamValue(InstanceVar, StepData.Function.Instance);
                        returnValue = Method.Invoke(instance, Params);
                        if (CoreUtils.IsValidVaraible(ReturnVar))
                        {
                            Context.VariableMapper.SetParamValue(ReturnVar, StepData.Function.Return, returnValue,
                                StepData.RecordStatus);
                            LogTraceVariable(StepData.Function.Return, returnValue);
                        }
                        break;
                    case FunctionType.StaticFunction:
                        returnValue = Method.Invoke(null, Params);
                        if (CoreUtils.IsValidVaraible(ReturnVar))
                        {
                            Context.VariableMapper.SetParamValue(ReturnVar, StepData.Function.Return, returnValue,
                                StepData.RecordStatus);
                            LogTraceVariable(StepData.Function.Return, returnValue);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                // 更新所有被ref修饰的变量类型的值
                UpdateParamVariableValue();
            }
        }

        // 因为Variable的值在整个过程中会变化，所以需要在运行前实时获取
        private void SetVariableParamValue()
        {
            IParameterDataCollection parameters = StepData.Function.Parameters;
            if (null == parameters)
            {
                return;
            }
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].ParameterType == ParameterType.Variable)
                {
                    // 获取变量值的名称，该名称为变量的运行时名称，其值在InitializeParamValue方法里配置
                    string variableName = ModuleUtils.GetVariableNameFromParamValue(parameters[i].Value);
                    // 使用变量名称获取变量当前对象的值
                    object variableValue = Context.VariableMapper.GetParamValue(variableName, parameters[i].Value);
                    // 根据ParamString和变量对应的值配置参数。
                    Params[i] = ModuleUtils.GetParamValue(parameters[i].Value, variableValue);
                }
            }
        }

        // 更新所有被ref或out修饰的参数值。如果变量的LogRecordLevel为Trace，则将更新的值写入日志。
        private void UpdateParamVariableValue()
        {
            for (int i = 0; i < Params.Length; i++)
            {
                IArgument argument = StepData.Function.ParameterType[i];
                IParameterData parameter = StepData.Function.Parameters[i];
                // 如果参数值是直接传递值，或者参数没有使用ref或out修饰，则返回
                if (parameter.ParameterType == ParameterType.Value || argument.Modifier == ArgumentModifier.None)
                {
                    continue;
                }
                object value = Params[i];
                // variableName已经是运行时名称
                string runtimeVariableName = ModuleUtils.GetVariableNameFromParamValue(parameter.Value);
                Context.VariableMapper.SetParamValue(runtimeVariableName, parameter.Value, value, 
                    StepData.RecordStatus);
                IVariable variable = CoreUtils.GetVariable(Context.Sequence, runtimeVariableName);
                if (variable.LogRecordLevel == RecordLevel.Trace)
                {
                    LogTraceVariable(variable, value);
                }
            }
        }

        private void LogTraceVariable(IVariable variable, object value)
        {
            const string variableLogFormat = "[Variable Trace] Name:{0}, Stack:{1}, Value: {2}.";
            string stackStr = GetStack().ToString();
            string varValueStr;
            if (null != value)
            {
                varValueStr = variable.VariableType == VariableType.Class
                    ? JsonConvert.SerializeObject(value)
                    : value.ToString();
            }
            else
            {
                varValueStr = CoreConstants.NullValue;
            }
            string printStr = string.Format(variableLogFormat, variable.Name, stackStr, varValueStr);
            Context.LogSession.Print(LogLevel.Info, Context.SessionId, printStr);
        }

        private void LogTraceVariable(string varString, object value)
        {
            string variableName = ModuleUtils.GetVariableNameFromParamValue(varString);
            IVariable variable = ModuleUtils.GetVaraibleByRawVarName(variableName, StepData);
            if (variable.LogRecordLevel == RecordLevel.Trace)
            {
                LogTraceVariable(variable, value);
            }
        }
    }
}