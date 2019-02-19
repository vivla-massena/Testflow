﻿using System;
using System.Xml.Serialization;
using Testflow.Data;
using Testflow.Data.Description;
using Testflow.Data.Sequence;
using Testflow.SequenceManager.Common;

namespace Testflow.SequenceManager.SequenceElements
{
    [Serializable]
    internal class FunctionData : IFunctionData
    {
        public FunctionData()
        {
            this.Type = FunctionType.StaticFunction;
            this.MethodName = string.Empty;
            this.ClassType = null;
            this.ClassTypeIndex = Constants.UnverifiedTypeIndex;
            this.ParameterType = null;
            this.Parameters = new ParameterDataCollection();
            this.Instance = string.Empty;
            this.Return = string.Empty;
            this.ReturnType = null;
            this.Description = null;
        }

        public FunctionType Type { get; set; }
        public string MethodName { get; set; }

        [XmlIgnore]
        [SerializationIgnore]
        public ITypeData ClassType { get; set; }

        public int ClassTypeIndex { get; set; }
        public IArgumentCollection ParameterType { get; set; }
        public IParameterDataCollection Parameters { get; set; }
        public string Instance { get; set; }
        public string Return { get; set; }
        public IArgument ReturnType { get; set; }

        [XmlIgnore]
        [SerializationIgnore]
        public IFuncInterfaceDescription Description { get; set; }

        public IFunctionData Clone()
        {
            ArgumentCollection parameterType = new ArgumentCollection();
            Common.Utility.CloneCollection(ParameterType, parameterType);

            ParameterDataCollection parameters = new ParameterDataCollection();
            Common.Utility.CloneCollection(Parameters, parameters);

            FunctionData functionData = new FunctionData()
            {
                Type = this.Type,
                MethodName = this.MethodName,
                ClassType = this.ClassType,
                ClassTypeIndex = this.ClassTypeIndex,
                ParameterType = parameterType,
                Parameters = parameters,
                Instance = this.Instance,
                Return = this.Return,
                ReturnType = this.ReturnType.Clone(),
                Description = this.Description
            };
            return functionData;
        }

        public void Initialize(IFuncInterfaceDescription funcInterface)
        {
            ArgumentCollection argumentsTypes = new ArgumentCollection();
            foreach (IArgumentDescription argumentDescription in funcInterface.Arguments)
            {
                Argument argumentData = new Argument();
                argumentData.Initialize(argumentDescription);
                argumentsTypes.Add(argumentData);
            }

            ParameterDataCollection parameters = new ParameterDataCollection();
            foreach (IArgumentDescription argumentDescription in funcInterface.Arguments)
            {
                parameters.Add(new ParameterData());
            }

            Argument returnType = new Argument();
            returnType.Initialize(funcInterface.Return);

            Type = funcInterface.FuncType;
            MethodName = funcInterface.Name;
            ClassType = funcInterface.ClassType;
            Description = funcInterface;
            Instance = string.Empty;
            Parameters = parameters;
            ParameterType = argumentsTypes;
            ReturnType = returnType;
        }
    }
}