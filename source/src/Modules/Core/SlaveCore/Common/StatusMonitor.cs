﻿using System;
using System.Threading;
using Testflow.Common;
using Testflow.CoreCommon.Common;
using Testflow.CoreCommon.Data;
using Testflow.CoreCommon.Messages;
using Testflow.Runtime;
using Testflow.SlaveCore.Data;

namespace Testflow.SlaveCore.Common
{
    /// <summary>
    /// 上行消息包装器，如果指定时间内无消息上报，则自动上传中间状态消息
    /// </summary>
    internal class StatusMonitor : IDisposable
    {
        private readonly SlaveContext _context;
        private readonly MessageTransceiver _transceiver;
        private readonly Timer _waitTimer;
        private readonly int _heartBeatInterval;
        private Thread _statusPackageThd;
        private CancellationTokenSource _cancellation;

        public Func<MessageBase> HeartbeatMsgGenerator { get; set; }

        public StatusMonitor(SlaveContext context)
        {
            this._transceiver = _context.MessageTransceiver;
            this._context = context;
            this._waitTimer = new Timer(SendHeartBeatMessage, null, Timeout.Infinite, Timeout.Infinite);
            this._heartBeatInterval = _context.GetProperty<int>("StatusUploadInterval");
        }

        public void Start()
        {
            this._cancellation = new CancellationTokenSource();
            _statusPackageThd = new Thread(PackageStatusInfo)
            {
                IsBackground = true,
                Name = string.Format(Constants.StatusMonitorThread, _context.SessionId)
            };
            _statusPackageThd.Start();
            _waitTimer.Change(_heartBeatInterval, _heartBeatInterval);
        }

        public void Stop()
        {
            _waitTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _cancellation?.Cancel();
        }

        private void PackageStatusInfo()
        {
            LocalEventQueue<SequenceStatusInfo> statusQueue = _context.StatusQueue;
            while (!_cancellation.IsCancellationRequested)
            {
                SequenceStatusInfo statusInfo = statusQueue.WaitUntilMessageCome();
                _waitTimer.Change(_heartBeatInterval, _heartBeatInterval);
                SendStatusMessage(statusInfo);
            }
        }

        private void SendStatusMessage(SequenceStatusInfo statusInfo)
        {
            if (statusInfo.Sequence == CommonConst.PlatformSession)
            {
                switch (statusInfo.ReportType)
                {
                    case StatusReportType.Start:
                        break;
                    case StatusReportType.Over:
                        break;
                    case StatusReportType.Error:
                        break;
                    default:
                        throw new InvalidProgramException();
                }
            }
            else
            {
                SendSequenceStatusMessage(statusInfo);
            }
        }

        private void SendSequenceStatusMessage(SequenceStatusInfo statusInfo)
        {
            StatusMessage statusMessage = null;
            switch (statusInfo.ReportType)
            {
                case StatusReportType.Start:
                    statusMessage = new StatusMessage(MessageNames.ReportStatusName, _context.State, _context.SessionId)
                    {
                        Time = statusInfo.Time, Index = _context.MsgIndex
                    };

                    statusMessage.InterestedSequence.Add(statusInfo.Sequence);
                    statusMessage.Stacks.Add(statusInfo.Stack);
                    statusMessage.SequenceStates.Add(RuntimeState.Running);
                    statusMessage.Results.Add(statusInfo.Result);
                    _transceiver.SendMessage(statusMessage);
                    break;
                case StatusReportType.Record:
                    statusMessage = new StatusMessage(MessageNames.ReportStatusName, _context.State, _context.SessionId)
                    {
                        Time = statusInfo.Time, Index = _context.MsgIndex
                    };
                    statusMessage.InterestedSequence.Add(statusInfo.Sequence);
                    statusMessage.Stacks.Add(statusInfo.Stack);
                    statusMessage.SequenceStates.Add(RuntimeState.Running);
                    statusMessage.Results.Add(statusInfo.Result);
                    if (statusInfo.Exception != null && statusInfo.ReportType == StatusReportType.Error)
                    {
                        statusMessage.FailedInfo.Add(statusInfo.Sequence, statusInfo.Exception.Message);
                    }
                    _transceiver.SendMessage(statusMessage);
                    break;
                case StatusReportType.DebugHitted:
                    DebugMessage debugMessage = new DebugMessage(MessageNames.UpDebugMsgName, statusInfo.Sequence, DebugMessageType.BreakPointHitted)
                    {
                        BreakPoint = statusInfo.Stack,
                        Time = statusInfo.Time,
                        Index = _context.MsgIndex
                    };
                    _transceiver.SendMessage(debugMessage);
                    break;
                case StatusReportType.Failed:
                    statusMessage = new StatusMessage(MessageNames.ReportStatusName, _context.State, _context.SessionId)
                    {
                        Time = statusInfo.Time, Index = _context.MsgIndex
                    };
                    statusMessage.InterestedSequence.Add(statusInfo.Sequence);
                    statusMessage.Stacks.Add(statusInfo.Stack);
                    statusMessage.SequenceStates.Add(RuntimeState.Failed);
                    statusMessage.Results.Add(statusInfo.Result);
                    if (statusInfo.Exception != null && statusInfo.ReportType == StatusReportType.Error)
                    {
                        statusMessage.FailedInfo.Add(statusInfo.Sequence, statusInfo.Exception.Message);
                    }
                    _transceiver.SendMessage(statusMessage);
                    break;
                case StatusReportType.Over:
                    statusMessage = new StatusMessage(MessageNames.ReportStatusName, _context.State, _context.SessionId)
                    {
                        Time = statusInfo.Time, Index = _context.MsgIndex
                    };
                    statusMessage.InterestedSequence.Add(statusInfo.Sequence);
                    statusMessage.Stacks.Add(statusInfo.Stack);
                    statusMessage.SequenceStates.Add(RuntimeState.Success);
                    statusMessage.Results.Add(statusInfo.Result);
                    _transceiver.SendMessage(statusMessage);
                    break;
                case StatusReportType.Error:
                    statusMessage = new StatusMessage(MessageNames.ReportStatusName, _context.State, _context.SessionId)
                    {
                        Time = statusInfo.Time, Index = _context.MsgIndex
                    };
                    statusMessage.InterestedSequence.Add(statusInfo.Sequence);
                    statusMessage.Stacks.Add(statusInfo.Stack);
                    statusMessage.SequenceStates.Add(RuntimeState.Error);
                    statusMessage.Results.Add(statusInfo.Result);
                    if (statusInfo.Exception != null && statusInfo.ReportType == StatusReportType.Error)
                    {
                        statusMessage.FailedInfo.Add(statusInfo.Sequence, statusInfo.Exception.Message);
                    }
                    _transceiver.SendMessage(statusMessage);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void SendHeartBeatMessage(object state)
        {
            bool cancellationRequested = _cancellation.IsCancellationRequested;
            if (cancellationRequested || null == HeartbeatMsgGenerator)
            {
                return;
            }
            MessageBase message = HeartbeatMsgGenerator.Invoke();
            if (null != message)
            {
                _transceiver.SendMessage(message);
            }
        }

        public void SendMessage(MessageBase message)
        {
            _transceiver.SendMessage(message);
            if (!_cancellation.IsCancellationRequested)
            {
                _waitTimer.Change(_heartBeatInterval, _heartBeatInterval);
            }
        }


        public void Dispose()
        {
            _waitTimer.Dispose();
        }
    }
}