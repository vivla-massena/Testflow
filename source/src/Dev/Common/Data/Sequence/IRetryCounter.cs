﻿using System.Xml.Serialization;
using Testflow.Usr;

namespace Testflow.Data.Sequence
{
    /// <summary>
    /// 重试计数器
    /// </summary>
    public interface IRetryCounter : ICloneableClass<IRetryCounter>, ISequenceElement
    {
        /// <summary>
        /// 重试计数变量名称
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 计数器的最大重试次数
        /// </summary>
        int MaxRetryTimes { get; set; }

        /// <summary>
        /// 成功次数：达到指定成功次数时认定为成功
        /// </summary>
        int PassTimes { get; set; }

        /// <summary>
        /// Retry功能是否使能
        /// </summary>
        bool RetryEnabled { get; set; }

        /// <summary>
        /// 记录当前的重试次数的变量
        /// </summary>
        string CounterVariable { get; set; }

        /// <summary>
        /// 记录当前的执行成功次数的变量
        /// </summary>
        string PassCountVariable { get; set; }
    }
}