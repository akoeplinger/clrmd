﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class ClrEnum : IClrEnum
    {
        public ClrType Type { get; }
        IClrType IClrEnum.Type => Type;

        public ClrElementType ElementType { get; }

        private readonly (string Name, object? Value)[] _values;

        internal ClrEnum(ClrType type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum)
                throw new InvalidOperationException($"{type.Name ?? nameof(ClrType)} is not an enum.  You must call {nameof(ClrType)}.{nameof(ClrType.IsEnum)} before using {nameof(ClrEnum)}.");

            IAbstractMetadataReader? import = type.Module.MetadataReader;
            if (import != null)
            {
                _values = EnumerateValues(import, out ClrElementType elementType).ToArray();
                ElementType = elementType;
            }
            else
            {
                _values = Array.Empty<(string Name, object? Value)>();
            }
        }

        public T GetEnumValue<T>(string name) where T : unmanaged
        {
            object? value = _values.Single(v => v.Name == name).Value;
            return value is null ? throw new InvalidOperationException($"Enum {Type.Name} had null '{name}' value.") : (T)value;
        }

        public IEnumerable<string> GetEnumNames() => _values.Select(v => v.Name);
        public IEnumerable<(string Name, object? Value)> EnumerateValues() => _values;

        private (string Name, object? Value)[] EnumerateValues(IAbstractMetadataReader import, out ClrElementType elementType)
        {
            List<(string Name, object? Value)> values = new();
            elementType = ClrElementType.Unknown;

            foreach (FieldDefInfo info in import.EnumerateFields(Type.MetadataToken))
            {
                if (info.Name == null)
                    continue;

                if ((int)info.Attributes == 0x606 && info.Name == "value__")
                {
                    SigParser parser = new(info.Signature, info.SignatureSize);
                    if (parser.GetCallingConvInfo(out _) && parser.GetElemType(out int elemType))
                        elementType = (ClrElementType)elemType;
                }

                // public, static, literal, has default
                if ((int)info.Attributes == 0x8056)
                {
                    SigParser parser = new(info.Signature, info.SignatureSize);
                    parser.GetCallingConvInfo(out _);
                    parser.GetElemType(out int _);

                    object? o = GetValueForPointer((ClrElementType)info.Type, info.ValuePointer);
                    values.Add((info.Name, o));
                }
            }

            return values.ToArray();
        }

        private unsafe object? GetValueForPointer(ClrElementType pdwCPlusTypeFlag, IntPtr ppValue) => pdwCPlusTypeFlag switch
        {
            ClrElementType.Boolean => *(byte*)ppValue,
            ClrElementType.Char => *(char*)ppValue,
            ClrElementType.Double => *(double*)ppValue,
            ClrElementType.Float => *(float*)ppValue,
            ClrElementType.Int8 => *(sbyte*)ppValue,
            ClrElementType.Int16 => *(short*)ppValue,
            ClrElementType.Int32 => *(int*)ppValue,
            ClrElementType.Int64 => *(long*)ppValue,
            ClrElementType.UInt8 => *(byte*)ppValue,
            ClrElementType.UInt16 => *(ushort*)ppValue,
            ClrElementType.UInt32 => *(uint*)ppValue,
            ClrElementType.UInt64 => *(ulong*)ppValue,
            ClrElementType.NativeInt => *(nint*)ppValue,
            ClrElementType.NativeUInt => *(nuint*)ppValue,
            _ => null,
        };
    }
}