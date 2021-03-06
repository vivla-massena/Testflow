﻿using System;
using System.Collections.Generic;
using Testflow.Usr;
using Testflow.CoreCommon.Common;
using Testflow.CoreCommon.Messages;
using Testflow.Data.Sequence;
using Testflow.Runtime;
using Testflow.SlaveCore.Common;
using Testflow.Data;
using Testflow.SlaveCore.Coroutine;

namespace Testflow.SlaveCore.Runner.Model
{
    internal class SessionTaskEntity
    {
        private readonly SlaveContext _context;

        private readonly SequenceTaskEntity _setUp;

        private readonly SequenceTaskEntity _tearDown;

        private readonly List<SequenceTaskEntity> _sequenceEntities;

        public int SequenceCount => _sequenceEntities.Count;

        public SessionTaskEntity(SlaveContext context)
        {
            this._context = context;

            ISequenceFlowContainer sequenceData = _context.Sequence;
            switch (context.SequenceType)
            {
                case RunnerType.TestProject:
                    ITestProject testProject = (ITestProject)sequenceData;
                    _setUp = new SequenceTaskEntity(testProject.SetUp, _context);
                    _tearDown = new SequenceTaskEntity(testProject.TearDown, _context);
                    _sequenceEntities = new List<SequenceTaskEntity>(1);
                    break;
                case RunnerType.SequenceGroup:
                    ISequenceGroup sequenceGroup = (ISequenceGroup)sequenceData;
                    _setUp = new SequenceTaskEntity(sequenceGroup.SetUp, _context);
                    _tearDown = new SequenceTaskEntity(sequenceGroup.TearDown, _context);
                    _sequenceEntities = new List<SequenceTaskEntity>(sequenceGroup.Sequences.Count);
                    foreach (ISequence sequence in sequenceGroup.Sequences)
                    {
                        _sequenceEntities.Add(new SequenceTaskEntity(sequence, _context));
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        public void Generate(ExecutionModel executionModel)
        {
            CoroutineHandle defaultCoroutine = _context.CoroutineManager.GetNextCoroutine();
            _context.TimingManager.RegisterStopWatch(defaultCoroutine.Id);
            _setUp.Generate(defaultCoroutine.Id);
            _tearDown.Generate(defaultCoroutine.Id);
            switch (executionModel)
            {
                case ExecutionModel.SequentialExecution:
                    foreach (SequenceTaskEntity sequenceModel in _sequenceEntities)
                    {
                        sequenceModel.Generate(defaultCoroutine.Id);
                    }
                    break;
                case ExecutionModel.ParallelExecution:
                    CoroutineHandle coroutine = _context.CoroutineManager.GetNextCoroutine();
                    foreach (SequenceTaskEntity sequenceModel in _sequenceEntities)
                    {
                        _context.TimingManager.RegisterStopWatch(coroutine.Id);
                        sequenceModel.Generate(coroutine.Id);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(executionModel), executionModel, null);
            }
        }

        public void InvokeSetUp()
        {
            _setUp.Invoke();
        }

        public void InvokeTearDown()
        {
            _tearDown.Invoke(true);
        }

        public void InvokeSequence(int index)
        {
            _sequenceEntities[index].Invoke();
        }

        public RuntimeState GetSequenceTaskState(int index)
        {
            switch (index)
            {
                case CommonConst.SetupIndex:
                    return _setUp.State;
                    break;
                case CommonConst.TeardownIndex:
                    return _tearDown.State;
                    break;
                default:
                    return _sequenceEntities[index].State;
                    break;
            }
        }

        public SequenceTaskEntity GetSequenceTaskEntity(int index)
        {
            switch (index)
            {
                case CommonConst.SetupIndex:
                    return _setUp;
                    break;
                case CommonConst.TeardownIndex:
                    return _tearDown;
                    break;
                default:
                    return _sequenceEntities[index];
                    break;
            }
        }

        /// <summary>
        /// 心跳包中填充状态
        /// </summary>
        public void FillSequenceInfo(StatusMessage message)
        {
            _setUp.FillStatusInfo(message);
            foreach (SequenceTaskEntity sequenceTaskEntity in _sequenceEntities)
            {
                sequenceTaskEntity.FillStatusInfo(message);
            }
            _tearDown.FillStatusInfo(message);
        }

        /// <summary>
        /// 全局失败后填充状态
        /// </summary>
        public void FillSequenceInfo(StatusMessage message, string errorMessage)
        {
            _setUp.FillStatusInfo(message, errorMessage);
            foreach (SequenceTaskEntity sequenceTaskEntity in _sequenceEntities)
            {
                sequenceTaskEntity.FillStatusInfo(message, errorMessage);
            }
            _tearDown.FillStatusInfo(message, errorMessage);
        }
    }
}