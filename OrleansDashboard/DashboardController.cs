﻿using Microsoft.Owin;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrleansDashboard
{

    public class DashboardController 
    {
        public TaskScheduler TaskScheduler { get; private set; }
        public IProviderRuntime ProviderRuntime { get; private set; }


        public DashboardController(Router router, TaskScheduler taskScheduler, IProviderRuntime providerRuntime)
        {

            this.TaskScheduler = taskScheduler;
            this.ProviderRuntime = providerRuntime;

            Action<string, Func<IOwinContext, IDictionary<string, string>, Task>> add = router.Add;

            add("/", Index);
            add("/index.min.js", IndexJs);
            add("/DashboardCounters", GetDashboardCounters);
            add("/RuntimeStats/:address", GetRuntimeStats);
            add("/HistoricalStats/:address", GetHistoricalStats);
            add("/GrainStats/:grain", GetGrainStats);
            add("/SiloProperties/:address", GetSiloExtendedProperties);
        }

        Task Index(IOwinContext context, IDictionary<string,string> parameters)
        {
            return context.ReturnFile("Index.html", "text/html");
        }

        Task IndexJs(IOwinContext context, IDictionary<string, string> parameters)
        {
            return context.ReturnFile("index.min.js", "application/javascript");
        }

        async Task GetDashboardCounters(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grain = this.ProviderRuntime.GrainFactory.GetGrain<IDashboardGrain>(0);

            var result = await Dispatch(async () => {
                return await grain.GetCounters().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await context.ReturnJson(result).ConfigureAwait(false);
        }

        async Task GetRuntimeStats(IOwinContext context, IDictionary<string, string> parameters)
        {
            var address = SiloAddress.FromParsableString(parameters["address"]);
            var grain = this.ProviderRuntime.GrainFactory.GetGrain<IManagementGrain>(0);
            
            var result = await Dispatch(async () =>
            {
                Dictionary<SiloAddress, SiloStatus> silos = await grain.GetHosts(true).ConfigureAwait(false);
                
                SiloStatus siloStatus;
                if (silos.TryGetValue(address, out siloStatus))
                {
                    return (await grain.GetRuntimeStatistics(new SiloAddress[] { address }).ConfigureAwait(false)).FirstOrDefault();
                }
                return null;
            }).ConfigureAwait(false);


            await context.ReturnJson(result).ConfigureAwait(false);
        }

        async Task GetHistoricalStats(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grain = this.ProviderRuntime.GrainFactory.GetGrain<ISiloGrain>(parameters["address"]);

            var result = await Dispatch(async () =>
            {
                return await grain.GetRuntimeStatistics().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await context.ReturnJson(result).ConfigureAwait(false);
        }

        async Task GetSiloExtendedProperties(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grain = this.ProviderRuntime.GrainFactory.GetGrain<ISiloGrain>(parameters["address"]);

            var result = await Dispatch(async () =>
            {
                return await grain.GetExtendedProperties().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await context.ReturnJson(result).ConfigureAwait(false);
        }

        async Task GetGrainStats(IOwinContext context, IDictionary<string, string> parameters)
        {
            var grainName = parameters["grain"];
            var grain = this.ProviderRuntime.GrainFactory.GetGrain<IDashboardGrain>(0);

            var result = await Dispatch(async () =>
            {
                return await grain.GetGrainTracing(grainName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await context.ReturnJson(result).ConfigureAwait(false);
        }

        Task<object> Dispatch(Func<Task<object>> func)
        {
            return Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, scheduler: this.TaskScheduler).Result;
        }

    }
}
