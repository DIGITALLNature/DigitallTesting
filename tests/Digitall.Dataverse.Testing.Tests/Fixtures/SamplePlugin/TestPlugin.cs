// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.Tests.Fixtures.SamplePlugin;

// ReSharper disable once UnusedType.Global
public class TestPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var tracingService = serviceProvider.GetService(typeof(ITracingService)) as ITracingService;
        tracingService?.Trace("TestPlugin: Execute");
    }
}
