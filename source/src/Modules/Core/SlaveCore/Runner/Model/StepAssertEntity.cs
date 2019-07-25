﻿using Testflow.CoreCommon.Messages;
using Testflow.Data.Sequence;
using Testflow.SlaveCore.Common;
using Testflow.SlaveCore.Data;

namespace Testflow.SlaveCore.Runner.Model
{
    internal class StepAssertEntity : StepTaskEntityBase
    {
        public string VariableName { get; }

        public string RuntimeVariableName { get; }

        public string Expected { get; }

        public string RealValue { get; }

        public StepAssertEntity(ISequenceStep step, SlaveContext context, int sequenceIndex) : base(step, context, sequenceIndex)
        {

        }

        public override void GenerateInvokeInfo()
        {
            throw new System.NotImplementedException();
        }

        public override void InitializeParamsValues()
        {
            throw new System.NotImplementedException();
        }

        protected override void InvokeStep(bool forceInvoke)
        {
            throw new System.NotImplementedException();
        }
    }
}