using System;
using System.Threading.Tasks;

using Synapse.Core;
using Synapse.Common.WebApi;
using System.Collections.Generic;

namespace Synapse.Services
{
    public class NodeServiceHttpApiClient : HttpApiClientBase
    {
        string _rootPath = "/synapse/node";

        public NodeServiceHttpApiClient(string baseUrl, string messageFormatType = "application/json") : base( baseUrl, messageFormatType )
        {
        }


        public ExecuteResult StartPlan(int planInstanceId, bool dryRun, Plan plan)
        {
            return StartPlanAsync( planInstanceId, dryRun, plan ).Result;
        }

        public async Task<ExecuteResult> StartPlanAsync(int planInstanceId, bool dryRun, Plan plan)
        {
            string requestUri = $"{_rootPath}/execute/{planInstanceId}/?action=start&dryRun={dryRun}";
            return await PostAsync<Plan, ExecuteResult>( plan, requestUri );
        }

        public void CancelPlan(long planInstanceId)
        {
            CancelPlanAsync( planInstanceId ).Wait();
        }

        public async Task CancelPlanAsync(long planInstanceId)
        {
            string requestUri = $"{_rootPath}/execute/{planInstanceId}/?action=cancel";
            await GetAsync( requestUri );
        }


        public void Drainstop(bool shutdown)
        {
            DrainstopAsync( shutdown ).Wait();
        }

        public async Task DrainstopAsync(bool shutdown)
        {
            string requestUri = $"{_rootPath}/drainstop/?action=stop&shutdown={shutdown}";
            await GetAsync( requestUri );
        }

        public void Undrainstop()
        {
            UndrainstopAsync().Wait();
        }

        public async Task UndrainstopAsync()
        {
            string requestUri = $"{_rootPath}/drainstop/?action=unstop";
            await GetAsync( requestUri );
        }

        public bool GetIsDrainstopComplete() { return GetIsDrainstopCompleteAsync().Result; }

        public async Task<bool> GetIsDrainstopCompleteAsync()
        {
            string requestUri = $"{_rootPath}/drainstop/?action=status";
            return await GetAsync<bool>( requestUri );
        }

        public int GetCurrentQueueDepth() { return GetCurrentQueueDepthAsync().Result; }

        public async Task<int> GetCurrentQueueDepthAsync()
        {
            string requestUri = $"{_rootPath}/drainstop/?action=depth";
            return await GetAsync<int>( requestUri );
        }

        public List<string> GetCurrentQueueItems() { return GetCurrentQueueItemsAsync().Result; }

        public async Task<List<string>> GetCurrentQueueItemsAsync()
        {
            string requestUri = $"{_rootPath}/drainstop/?action=list";
            return await GetAsync<List<string>>( requestUri );
        }
    }
}