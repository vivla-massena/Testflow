﻿using System;
using System.Collections;
using Testflow.Data.Expression;

namespace Testflow.External.ExpressionCalculators
{
    public class Array3DElementAcquirer : IExpressionCalculator
    {
        public string Operator { get; }

        public Array3DElementAcquirer()
        {
            this.Operator = "Get3DArrayElement";
        }

        public bool IsCalculable(object sourceValue, params object[] arguments)
        {
            Array sourceArray = sourceValue as Array;
            return sourceArray?.Rank == 3;
        }

        public object Calculate(object sourceValue, params object[] arguments)
        {
            Array sourceArray = sourceValue as Array;
            return sourceArray.GetValue((int) arguments[0], (int) arguments[1], (int)arguments[1]);
        }
    }
}