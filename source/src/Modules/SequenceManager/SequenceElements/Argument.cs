﻿using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Testflow.Data;
using Testflow.Data.Description;
using Testflow.Data.Sequence;
using Testflow.SequenceManager.Common;

namespace Testflow.SequenceManager.SequenceElements
{
    [Serializable]
    public class Argument : IArgument
    {
        public Argument()
        {
            this.Name = "";
            this.Type = null;
            this.TypeIndex = Constants.UnverifiedTypeIndex;
            this.Modifier = ArgumentModifier.None;
            this.VariableType = VariableType.Undefined;
        }

        public string Name { get; set; }

        [XmlIgnore]
        [SerializationIgnore]
        [RuntimeSerializeIgnore]
        public ITypeData Type { get; set; }

        public int TypeIndex { get; set; }

        public ArgumentModifier Modifier { get; set; }

        public VariableType VariableType { get; set; }

        public IArgument Clone()
        {
            Argument argument = new Argument()
            {
                Name = this.Name,
                Type = this.Type,
                TypeIndex = this.TypeIndex,
                Modifier = this.Modifier,
                VariableType = this.VariableType
            };
            return argument;
        }

        public void Initialize(IArgumentDescription argumentDescription)
        {
            Modifier = argumentDescription.Modifier;
            Name = argumentDescription.Name;
            Type = argumentDescription.Type;
            VariableType = argumentDescription.ArgumentType;
        }

        #region 序列化声明及反序列化构造

        public Argument(SerializationInfo info, StreamingContext context)
        {
            ModuleUtils.FillDeserializationInfo(info, this, this.GetType());
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ModuleUtils.FillSerializationInfo(info, this);
        }

        #endregion
    }
}