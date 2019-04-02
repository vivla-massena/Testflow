﻿using System;

namespace Testflow.Runtime.Data
{
    /// <summary>
    /// 失败信息类
    /// </summary>
    public interface ISequenceFailedInfo
    {
        /// <summary>
        /// 失败类型
        /// </summary>
        FailedType Type { get; set; }

        /// <summary>
        /// 失败的异常信息
        /// </summary>
        Exception FailedException { get; set; }

        /// <summary>
        /// 错误描述信息
        /// </summary>
        string Description { get; set; }
    }
}