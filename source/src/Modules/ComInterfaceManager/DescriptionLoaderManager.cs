﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Testflow.ComInterfaceManager.Data;
using Testflow.Data;
using Testflow.Data.Description;
using Testflow.Modules;
using Testflow.Usr;
using Testflow.Utility.I18nUtil;

namespace Testflow.ComInterfaceManager
{
    internal class DescriptionLoaderManager : IDisposable
    {
        private AppDomain _loaderDomain;
//        private int _assemblyCount;
        private readonly string _assemblyFullName;
        private readonly string _loaderName;
        private AssemblyDescriptionLoader _loader;
        private readonly ISequenceManager _sequenceManager;

        public DescriptionLoaderManager()
        {
            _loaderDomain = AppDomain.CreateDomain(Constants.AppDomainName);
            _assemblyFullName = this.GetType().Assembly.FullName;
            _loaderName = typeof(AssemblyDescriptionLoader).Name;
            _loader = (AssemblyDescriptionLoader)_loaderDomain.CreateInstanceAndUnwrap(_assemblyFullName, _loaderName);
            _sequenceManager = TestflowRunner.GetInstance().SequenceManager;
        }

        public ComInterfaceDescription LoadAssemblyDescription(IAssemblyInfo assemblyInfo, 
            DescriptionDataTable descriptionCollection)
        {
            ComInterfaceDescription assemblyDescription = _loader.LoadAssemblyDescription(assemblyInfo);
            CheckAssemblyDescription(assemblyDescription, assemblyInfo.AssemblyName);
            ModuleUtils.ValidateComDescription(_sequenceManager, assemblyDescription, descriptionCollection);
            descriptionCollection.Add(assemblyDescription);
            // 如果一个AppDomain载入过多的程序集，则卸载该AppDomain，构造新的AppDomain
//            if (_assemblyCount > Constants.MaxAssemblyCount)
//            {
//                AppDomain.Unload(_loaderDomain);
//                Thread.MemoryBarrier();
//                _loaderDomain = AppDomain.CreateDomain(Constants.AppDomainName);
//                _loader = (AssemblyDescriptionLoader)_loaderDomain.CreateInstanceAndUnwrap(_assemblyFullName, _loaderName);
//                Interlocked.Exchange(ref _assemblyCount, 0);
//            }
            return assemblyDescription;
        }

        public ComInterfaceDescription LoadAssemblyDescription(string path, DescriptionDataTable descriptionCollection)
        {
            IAssemblyInfo assemblyInfo = _sequenceManager.CreateAssemblyInfo();
            assemblyInfo.Path = path;
            ComInterfaceDescription assemblyDescription = _loader.LoadAssemblyDescription(assemblyInfo);
            CheckAssemblyDescription(assemblyDescription, assemblyInfo.AssemblyName);
            ModuleUtils.ValidateComDescription(_sequenceManager, assemblyDescription, descriptionCollection);
            descriptionCollection.Add(assemblyDescription);
            // 如果一个AppDomain载入过多的程序集，则卸载该AppDomain，构造新的AppDomain
//            if (_assemblyCount > Constants.MaxAssemblyCount)
//            {
//                AppDomain.Unload(_loaderDomain);
//                Thread.MemoryBarrier();
//                _loaderDomain = AppDomain.CreateDomain(Constants.AppDomainName);
//                _loader = (AssemblyDescriptionLoader)_loaderDomain.CreateInstanceAndUnwrap(_assemblyFullName, _loaderName);
//                Interlocked.Exchange(ref _assemblyCount, 0);
//            }
            return assemblyDescription;
        }

        public ITypeData GetPropertyType(ITypeData typeData, string property, DescriptionDataTable descriptionCollection)
        {
            ITypeDescription propertyTypeDescription = _loader.GetPropertyType(typeData, property);
            if (null == propertyTypeDescription)
            {
                CheckPropertyDescription(typeData, property);
            }
            string fullName = ModuleUtils.GetFullName(propertyTypeDescription);
            ITypeData propertyType;
            if (descriptionCollection.ContainsType(fullName))
            {
                propertyType = descriptionCollection.GetTypeData(fullName);
            }
            else
            {
                propertyType = _sequenceManager.CreateTypeData(propertyTypeDescription);
                descriptionCollection.AddTypeData(fullName, propertyType);
            }
            return propertyType;
        }

        private void CheckPropertyDescription(ITypeData typeData, string property)
        {
            if (null != _loader.Exception)
            {
                throw new TestflowRuntimeException(ModuleErrorCode.LibraryLoadError, _loader.Exception.Message,
                    _loader.Exception);
            }
            else
            {
                I18N i18N = I18N.GetInstance(Constants.I18nName);
                switch (_loader.ErrorCode)
                {
                    case ModuleErrorCode.LibraryLoadError:
                        throw new TestflowRuntimeException(ModuleErrorCode.LibraryLoadError,
                            i18N.GetStr("RuntimeError"));
                        break;
                    case ModuleErrorCode.PropertyNotFound:
                        throw new TestflowRuntimeException(ModuleErrorCode.PropertyNotFound,
                            i18N.GetFStr("PropertyNotFound", typeData.Name, property));
                        break;
                    case ModuleErrorCode.TypeCannotLoad:
                        throw new TestflowRuntimeException(ModuleErrorCode.TypeCannotLoad,
                            i18N.GetFStr("TypeNotFound", typeData.Name));
                        break;
                    default:
                        break;
                }
            }
        }

        private void CheckAssemblyDescription(ComInterfaceDescription description, string assemblyName)
        {
            I18N i18N = I18N.GetInstance(Constants.I18nName);
            ILogService logService = TestflowRunner.GetInstance().LogService;
            if (null == description && null != _loader.Exception)
            {
                throw new TestflowRuntimeException(ModuleErrorCode.LibraryLoadError,
                    i18N.GetFStr("RuntimeError", _loader.Exception.Message), _loader.Exception);
            }
            switch (_loader.ErrorCode)
            {
                case ModuleErrorCode.HighVersion:

                    logService.Print(LogLevel.Warn, CommonConst.PlatformLogSession,
                        $"The version of assembly '{assemblyName}' is higher than version defined in data.");
                    break;
                case ModuleErrorCode.LowVersion:
                    logService.Print(LogLevel.Error, CommonConst.PlatformLogSession,
                        $"The version of assembly '{assemblyName}' is lower than version defined in data.");
                    throw new TestflowRuntimeException(ModuleErrorCode.LowVersion,
                        i18N.GetFStr("LowAssemblyVersion", assemblyName));
                    break;
                case ModuleErrorCode.LibraryLoadError:
                    throw new TestflowRuntimeException(ModuleErrorCode.LibraryLoadError, i18N.GetStr("RuntimeError"));
                default:
                    break;
            }
        }

        public void Dispose()
        {
            AppDomain.Unload(_loaderDomain);
        }
    }
}