﻿using System;
using System.Runtime.Serialization;
using Testflow.Data;
using Testflow.Data.Sequence;
using Testflow.SequenceManager.Common;

namespace Testflow.SequenceManager.SequenceElements
{
    [Serializable]
    [RuntimeSerializeIgnore]
    public class OperationPanelInfo : IOperationPanelInfo
    {
        public IAssemblyInfo Assembly { get; set; }
        public ITypeData OperationPanelClass { get; set; }
        public IAssemblyInfo OIConfigAssembly { get; set; }
        public ITypeData OIConfigPanelClass { get; set; }
        public string Parameters { get; set; }

        public OperationPanelInfo()
        {
            this.Assembly = null;
            this.OperationPanelClass = null;
            this.Parameters = null;
            this.OIConfigAssembly = null;
            this.OIConfigPanelClass = null;
        }

        public OperationPanelInfo(SerializationInfo info, StreamingContext context)
        {
            ModuleUtils.FillDeserializationInfo(info, this, this.GetType());
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ModuleUtils.FillSerializationInfo(info, this);
        }
    }
}