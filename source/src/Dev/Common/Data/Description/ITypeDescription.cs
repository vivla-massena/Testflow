﻿using System.Collections.Generic;

namespace Testflow.Data.Description
{
    /// <summary>
    /// 类型描述信息
    /// </summary>
    public interface ITypeDescription
    {
        /// <summary>
        /// 类型所在配置集名称
        /// </summary>
        string AssemblyName { get; set; }

        /// <summary>
        /// 命名空间
        /// </summary>
        string Namespace { get; set; }

        /// <summary>
        /// 类型名称
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// 类的种类
        /// </summary>
        VariableType Kind { get; set; }

        /// <summary>
        /// 功能类别
        /// </summary>
        string Category { get; set; }

        /// <summary>
        /// 枚举值，只在类型为枚举时生效
        /// </summary>
        string[] Enumerations { get; set; }
    }
}