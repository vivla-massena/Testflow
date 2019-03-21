﻿using System;
using System.Collections.Generic;
using System.Threading;
using Testflow.Common;
using Testflow.CoreCommon.Common;
using Testflow.CoreCommon.Data.EventInfos;
using Testflow.Utility.I18nUtil;

namespace Testflow.CoreCommon.Data
{
    public class EventQueue : IDisposable
    {
        private readonly Queue<EventInfoBase> _events;
        private SpinLock _queueLock;

        public EventQueue()
        {
            _events = new Queue<EventInfoBase>(CoreConstants.DefaultEventsQueueSize);
            _queueLock = new SpinLock();
        }

        public void Enqueue(EventInfoBase item)
        {
            bool getLock = GetLock();
            if (_events.Count >= CoreConstants.MaxEventsQueueSize)
            {
                ReleaseLock(getLock);
                I18N i18N = I18N.GetInstance(CoreConstants.I18nName);
                throw new TestflowRuntimeException(ModuleErrorCode.EventsTooMany, i18N.GetStr("EventQueueOverflow"));
            }
            _events.Enqueue(item);
            ReleaseLock(getLock);
        }

        public EventInfoBase Dequeue()
        {
            bool getLock = GetLock();
            EventInfoBase item = null;
            if (_events.Count > 0)
            {
                item = _events.Dequeue();
            }
            ReleaseLock(getLock);
            return item;
        }

        private bool GetLock()
        {
            bool getLock = false;
            _queueLock.TryEnter(CoreConstants.EventQueueTimeOut, ref getLock);
            if (!getLock)
            {
                I18N i18N = I18N.GetInstance(CoreConstants.I18nName);
                throw new TestflowRuntimeException(ModuleErrorCode.EventTimeOut, i18N.GetStr("EventEnqueueTimeOut"));
            }
            return true;
        }

        private void ReleaseLock(bool getLock)
        {
            if (getLock)
            {
                _queueLock.Exit();
            }
        }

        public void Dispose()
        {
            this._events.Clear();
        }
    }
}