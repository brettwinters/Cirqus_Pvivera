using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Events;
using d60.Cirqus.Logging;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.MongoDb.Views;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using MongoDB.Driver;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views.ViewManager
{
	
	#region
	
	public class Proposal : AggregateRoot, IEmit<ProposalCreated>, IEmit<ProposalLabelled>
	{
		public void LabelProposal(
			string label)
		{
			Emit(new ProposalLabelled { Label = label });
		}

		public List<string> Labels { get; set; } 

		protected override void Created()
		{
			Emit(new ProposalCreated());
		}

		public void Apply(
			ProposalLabelled e)
		{
			Labels.Add(e.Label);
		}
		
		public void Apply(ProposalCreated e)
		{
			Labels = new();
		}
	}

	public class ProposalCreated : DomainEvent<Proposal>
	{
		
	}

	public class ProposalLabelled : DomainEvent<Proposal>
	{
		public string Label { get; set; }
	}
	
	public class ProposalCommand : ExecutableCommand
	{
		public ProposalCommand(
			string proposalId, 
			string label)
		{
			ProposalId = proposalId;
			Label = label;
		}

		public string ProposalId { get; private set; }

		public string Label { get; private set; }

		public override void Execute(
			ICommandContext context)
		{
			var proposal = context.TryLoad<Proposal>(ProposalId) ?? context.Create<Proposal>(ProposalId);
			proposal.LabelProposal(Label);
		}
	}
	
	public class ProposalView : 
		IViewInstance<InstancePerAggregateRootLocator>, 
        ISubscribeTo<ProposalCreated>, 
        ISubscribeTo<ProposalLabelled> 
	{
		public string Id { get; set; }

		public long LastGlobalSequenceNumber { get; set; }

		public List<string> Labels { get; set; }

		public void Handle(
			IViewContext context, ProposalLabelled domainEvent)
		{
			Labels.Add(domainEvent.Label);
		}

		public void Handle(
			IViewContext context,
			ProposalCreated domainEvent)
		{
			Labels = new();
		}
	}
	
	public class ProposalSummaryView : 
		IViewInstance<InstancePerAggregateRootLocator>,
        ISubscribeTo<ProposalLabelled> 
	{
		public string Id { get; set; }

		public long LastGlobalSequenceNumber { get; set; }

		public int LabelCount { get; set; }

		public void Handle(
			IViewContext context, ProposalLabelled domainEvent)
		{
			LabelCount += 1;
		}
	}
	
	#endregion
	
    [TestFixture, Category(TestCategories.MongoDb)]
    public class TestSharedViewManagersForMultipleEventStreams : FixtureBase
    {

	    IMongoDatabase _mongoDatabase;

        protected override void DoSetUp()
        {
            _mongoDatabase = MongoHelper.InitializeTestDatabase();
        }
        
        [Test]
        public async Task ShouldBeAbleToShareSameViewForDifferentEventStreams()
        {
	        FakeGlobalSequenceNumberService.Reset();
	        var waitHandle = new ViewManagerWaitHandle();
	        var proposalViewManager = new MongoDbViewManager<ProposalView>(_mongoDatabase);
	        var proposalSummaryViewManager = new MongoDbViewManager<ProposalSummaryView>(_mongoDatabase);
	        ProposalView? proposalView = null;
	        ProposalSummaryView? proposalSummaryView = null;
	        
	        //------------------------------------------------------------------------------------
	        // oA logs in and dispatches 10 events for p1 for their own event store "Events1"
	        //------------------------------------------------------------------------------------
	        
	        var commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
			        .EventDispatcher(e => e
				        .UseViewManagerEventDispatcher(proposalViewManager, proposalSummaryViewManager)
				        .WithWaitHandle(waitHandle))
		        );
	        
	        CommandProcessingResult commandResult1 = null;
	        var numberOfCommands1 = 10;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        commandResult1 = commandProcessor.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    );
	        
	        await waitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalView = proposalViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalView);
	        Assert.AreEqual(
		        expected: 10,
		        actual: proposalView.Labels.Count
	        );
	        Assert.AreEqual(
		        1 + 9, //1 created + 10 events = 10
		        proposalView.LastGlobalSequenceNumber
	        );
	        
	        await waitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalSummaryView = proposalSummaryViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 10,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        1 + 9, //1 created + 10 events = 10
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        // User signs off
	        commandProcessor.Dispose();
	        
	        //------------------------------------------------------------------------------------
	        // oB logs in and dispatches 5 events for oBp1 for their own event store "Events2"
	        //------------------------------------------------------------------------------------
	        
	        proposalViewManager = new MongoDbViewManager<ProposalView>(_mongoDatabase);
	        commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events2"))
			        .EventDispatcher(e => e
				        .UseViewManagerEventDispatcher(proposalViewManager, proposalSummaryViewManager)
				        .WithWaitHandle(waitHandle))
		        );

	        // Now dispatch using a different processor, but same view
	        CommandProcessingResult commandResult2 = null;
	        var numberOfCommands2 = 5;
	        Enumerable.Range(0, numberOfCommands2).ToList().ForEach(i =>
		        commandResult2 = commandProcessor.ProcessCommand(new ProposalCommand($"oBp1", $"Label-2 {i}"))
	        );
	        
	        await waitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
	        proposalView = proposalViewManager.Load("oBp1");
	        
	        Assert.NotNull(proposalView);
	        Assert.AreEqual(
		        expected: 5,
		        actual: proposalView.Labels.Count
	        );
	        Assert.AreEqual(
		        1 + 9 + 1 + 5, //1 created + 10 events + 1 created + 5 events = 16
		        proposalView.LastGlobalSequenceNumber
	        );
	        
	        await waitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
	        
	        // The new AR is here
	        proposalSummaryView = proposalSummaryViewManager.Load("oBp1");
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 5,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        1 + 9 + 1 + 5, //1 created + 10 events + 1 created + 5 events = 16
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        // The old AR is here, note that the TimeStamp is the previous command result
	        proposalSummaryView = proposalSummaryViewManager.Load("oAp1");
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 10,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        1 + 9, //1 created + 10 events = 10
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        commandProcessor.Dispose();
	        
	        //------------------------------------------------------------------------------------
	        // oA logs in again and dispatches 20 events for p1 for their own event store "Events1"
	        //------------------------------------------------------------------------------------
	        
	        commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
			        .EventDispatcher(e => e
				        .UseViewManagerEventDispatcher(proposalViewManager, proposalSummaryViewManager)
				        .WithWaitHandle(waitHandle))
		        );
	        
	        numberOfCommands1 = 20;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        commandResult1 = commandProcessor.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    );
	        
	        await waitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalView = proposalViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalView);
	        Assert.AreEqual(
		        expected: 10 + 20,
		        actual: proposalView.Labels.Count
	        );
	        Assert.AreEqual(
		        1 + 9 + 1 + 5 + 20, //1 created + 10 events + 1 created + 5 events + 20 events = 37
		        proposalView.LastGlobalSequenceNumber
	        );
	        
	        await waitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalSummaryView = proposalSummaryViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 10 + 20,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        1 + 9 + 1 + 5 + 20, //1 created + 10 events + 1 created + 5 events + 20 events = 37
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        commandProcessor.Dispose();
        }
    }
}