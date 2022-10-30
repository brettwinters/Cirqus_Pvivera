using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Views;
using d60.Cirqus.Logging;
using d60.Cirqus.Logging.Console;
using d60.Cirqus.MongoDb.Config;
using d60.Cirqus.MongoDb.Views;
using d60.Cirqus.Numbers;
using d60.Cirqus.Serialization;
using d60.Cirqus.Testing;
using d60.Cirqus.Tests.MongoDb;
using d60.Cirqus.Views;
using d60.Cirqus.Views.ViewManagers;
using d60.Cirqus.Views.ViewManagers.Locators;
using MongoDB.Driver;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Views.NewViewManager
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
	
    //[TestFixture, Category(TestCategories.MongoDb)]
    public class TestTwoDispatchersOneView : FixtureBase
    {
	    ICommandProcessor _commandProcessor1;
        ICommandProcessor _commandProcessor2;
        
        IMongoDatabase _mongoDatabase;
        private IViewManager<ProposalView> _proposalViewManager;
        private ViewManagerWaitHandle _proposalWaitHandle;

        private IViewManager<ProposalSummaryView> _proposalSummaryViewManager;
        private ViewManagerWaitHandle _proposalSummaryWaitHandle;

        protected override void DoSetUp()
        {
            _mongoDatabase = MongoHelper.InitializeTestDatabase();
   //          //_viewManager = new MongoDbViewManager<ProposalView>(_mongoDatabase);
   //           _proposalViewManager = new SpecialInMemoryViewManager<ProposalView>();
   //           _proposalSummaryViewManager = new SpecialInMemoryViewManager<ProposalSummaryView>();
   //           _proposalWaitHandle = new ViewManagerWaitHandle();
   //           _proposalSummaryWaitHandle = new ViewManagerWaitHandle();
   //
			// // _commandProcessor1 = RegisterForDisposal(
			// // 	CreateCommandProcessor(config => config
			// // 		.Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			// // 		.EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
			// // 		.EventDispatcher(e => {
			// // 			e.UseViewManagerEventDispatcher(_proposalViewManager).WithWaitHandle(_proposalWaitHandle);
			// // 			e.UseViewManagerEventDispatcher(_proposalSummaryViewManager).WithWaitHandle(_proposalSummaryWaitHandle);
			// // 		})
			// // 	)
			// // );
			//
			//
   //           
   //           _commandProcessor1 = CreateCommandProcessor(config => config
	  //            .Logging(l => l.UseConsole(minLevel: Logger.Level.Warn))
	  //            .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
	  //            .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
		 //             var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
		 //             var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
		 //             var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
		 //             var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		 //             
		 //             var dispatcher = new SpecialViewManagerEventDispatcher(
			//              repository,
			//              eventStore,
			//              serializer,
			//              typeMapper,
			//              _proposalViewManager, _proposalSummaryViewManager) 
		 //             {
			//              AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
		 //             };
   //           
		 //             _proposalWaitHandle.Register(dispatcher);
		 //             _proposalSummaryWaitHandle.Register(dispatcher);
		 //             
		 //             return dispatcher;
	  //            }))
   //           );
   //          
   //           _commandProcessor2 = CreateCommandProcessor(config => config
	  //            .Logging(l => l.UseConsole(minLevel: Logger.Level.Warn))
	  //            .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events2"))
	  //            .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
		 //             var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
		 //             var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
		 //             var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
		 //             var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		 //             
		 //             var dispatcher = new SpecialViewManagerEventDispatcher(
			//              repository, 
			//              eventStore, 
			//              serializer, 
			//              typeMapper, 
			//              _proposalViewManager, _proposalSummaryViewManager) 
		 //             {
			//              AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
		 //             };
   //           
		 //             _proposalWaitHandle.Register(dispatcher);
		 //             _proposalSummaryWaitHandle.Register(dispatcher);
		 //             
		 //             return dispatcher;
	  //            }))
   //           );
   //          
			// // _commandProcessor2 = RegisterForDisposal(
			// //     CreateCommandProcessor(config => config
			// //         .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			// //         .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events2"))
			// //         .EventDispatcher(e => {
			// // 	        e.UseViewManagerEventDispatcher(_proposalViewManager).WithWaitHandle(_proposalWaitHandle);
			// // 	        e.UseViewManagerEventDispatcher(_proposalSummaryViewManager).WithWaitHandle(_proposalSummaryWaitHandle);
			// //         }
			// // 	))
			// // );
        }

        [Test]
        public async Task HowTheVariousNumbersWork()
        {
	        ProposalSummaryView proposalSummaryView = null;
	        ProposalView proposalView = null;
	        
	        // Act 1
	        var commandResult = _commandProcessor1.ProcessCommand(new ProposalCommand("oAp1", "abc"));
	        
	        await _proposalWaitHandle.WaitForAll(commandResult, TimeSpan.FromSeconds(30));
	        proposalView = _proposalViewManager.Load("oAp1");
	        
			Assert.NotNull(proposalView);
			Assert.AreEqual(
			    expected: new [] { "abc" },
			    actual: proposalView.Labels
			);
			Assert.AreEqual(
			    commandResult.TimeStamp,
			    proposalView.LastGlobalSequenceNumber
			);
	        
	        await _proposalSummaryWaitHandle.WaitForAll(commandResult, TimeSpan.FromSeconds(30));
	        proposalSummaryView = _proposalSummaryViewManager.Load("oAp1");
	        
			Assert.NotNull(proposalView);
			Assert.AreEqual(
			    expected: 1,
			    actual: proposalSummaryView.LabelCount
			);
			Assert.AreEqual(
			    commandResult.TimeStamp,
			    proposalSummaryView.LastGlobalSequenceNumber
			);
	        
	        // Act 2
	        // Uses another event stream 

	        commandResult = _commandProcessor2.ProcessCommand(new ProposalCommand("oBp1", "def"));
	        await _proposalWaitHandle.WaitForAll(commandResult, TimeSpan.FromSeconds(30));
	        
	        proposalView = _proposalViewManager.Load("oBp1");

			Assert.NotNull(proposalView);
			Assert.AreEqual(
			    expected: new [] { "def" },
			    actual: proposalView.Labels
			);
			Assert.AreEqual(
			    commandResult.TimeStamp,
			    proposalView.LastGlobalSequenceNumber
			);
	        
	        await _proposalSummaryWaitHandle.WaitForAll(commandResult, TimeSpan.FromSeconds(30));
	        proposalSummaryView = _proposalSummaryViewManager.Load("oBp1");
	        
			Assert.NotNull(proposalView);
			Assert.AreEqual(
			    expected: 2,
			    actual: proposalSummaryView.LabelCount
			);
			Assert.AreEqual(
			    commandResult.TimeStamp,
			    proposalSummaryView.LastGlobalSequenceNumber
			);
        }
        
        

        

      //   [Test]
      //   public void GivenSameAggregateRoot_AndSeparateEventStore_AndDifferentCommandProcessorsSameView()
      //   {
	     //    CommandProcessingResult lastCommandResult = null;
	     //    
	     //    // Dispatch 10 commands using the first command processor
	     //    var numberOfCommands1 = 10;
	     //    Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		    //     lastCommandResult = _commandProcessor1.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    // );
	     //    
	     //    //Command result NewPosition = 10 (10 events: 0, 1, ..9 )
	     //    Assert.That(lastCommandResult.GetNewPosition(), Is.EqualTo(10));
      //
	     //    _summaryView.WaitUntilProcessed(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();
      //
	     //    var viewOnFirstLoad1 = _summaryView.Load("oAp1");
	     //    Assert.That(viewOnFirstLoad1.LastGlobalSequenceNumber, Is.EqualTo(lastCommandResult.GetNewPosition()));
	     //    
	     //    // Now dispatch using a different processor, but same view
	     //    
	     //    var numberOfCommands2 = 5;
	     //    Enumerable.Range(0, numberOfCommands2).ToList().ForEach(i =>
		    //     lastCommandResult = _commandProcessor2.ProcessCommand(new ProposalCommand($"oAp1", $"Label-2 {i}"))
	     //    );
	     //    
	     //    //Command result NewPosition = 15 (15 events 0, 1.. 9 + 10, 11 .. 14 )
	     //    Assert.That(lastCommandResult.GetNewPosition(), Is.EqualTo(15));
	     //    
	     //    _summaryView.WaitUntilProcessed(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();
      //
	     //    var viewOnFirstLoad2 = _summaryView.Load("oAp1");
	     //    Assert.That(viewOnFirstLoad2.LastGlobalSequenceNumber, Is.EqualTo(15));
      //   }

        [Test]
        public void GivenSameAggregateRoot_AndSeparateEventStore_AndDifferentCommandProcessorsSameView()
        {

	        //var x = DateTime.UtcNow..ToUnixTimeMilliseconds();
	        CommandProcessingResult lastCommandResult = null;
	        
	        // Dispatch 10 commands using the first command processor
	        var numberOfCommands1 = 10;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        lastCommandResult = _commandProcessor1.ProcessCommand(new ProposalCommand("oAp1", $"Label-1 {i}"))
		    );
	        
	        //Command result NewPosition = 10 (10 events: 0, 1, ..9 )
	        //Assert.That(lastCommandResult.GetNewPosition(), Is.EqualTo(10));

	        _proposalWaitHandle.WaitForAll(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();
	        _proposalSummaryWaitHandle.WaitForAll(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();
	        
	        var viewOnFirstLoad1 = _proposalViewManager.Load("oAp1");
	        Assert.That(viewOnFirstLoad1.LastGlobalSequenceNumber, Is.EqualTo(10));
	        
	        var summaryView = _proposalSummaryViewManager.Load("oAp1");
	        Assert.That(summaryView.LastGlobalSequenceNumber, Is.EqualTo(10));
	        //Assert.That(viewOnFirstLoad1.LastGlobalSequenceNumber, Is.EqualTo(lastCommandResult.GetNewPosition()));
	        
	        // Now dispatch using a different processor, but same view
	        
	        var numberOfCommands2 = 5;
	        Enumerable.Range(0, numberOfCommands2).ToList().ForEach(i =>
		        lastCommandResult = _commandProcessor2.ProcessCommand(new ProposalCommand($"oBp1", $"Label-2 {i}"))
	        );
	        
	        //Command result NewPosition = 5 (5 events 0, 1..4 )
	        Assert.That(lastCommandResult.GetNewPosition(), Is.EqualTo(5));
	        
	        _proposalWaitHandle.WaitForAll(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();
	        _proposalSummaryWaitHandle.WaitForAll(lastCommandResult, TimeSpan.FromSeconds(30)).Wait();

	        summaryView = _proposalSummaryViewManager.Load("oAp1");
	        Assert.That(summaryView.LastGlobalSequenceNumber, Is.EqualTo(15));


	        var viewOnFirstLoad2 = _proposalViewManager.Load("oAp1");
	        Assert.That(viewOnFirstLoad2.LastGlobalSequenceNumber, Is.EqualTo(15));
        }
        
        
        [Test]
        public async Task InMemory_ThisIsHowItActuallyWorksInMercenta()
        {
	        var proposalWaitHandle = new ViewManagerWaitHandle();
	        var proposalSummaryWaitHandle = new ViewManagerWaitHandle();
	        var proposalViewManager = new SpecialInMemoryViewManager<ProposalView>();
	        var proposalSummaryViewManager = new SpecialInMemoryViewManager<ProposalSummaryView>();
	        ProposalView proposalView = null;
	        ProposalSummaryView proposalSummaryView = null;
	        
	        var commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
			        .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
				        var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
				        var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
				        var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
				        var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		             
				        var dispatcher = new SpecialViewManagerEventDispatcher(
					        repository,
					        eventStore,
					        serializer,
					        typeMapper,
					        proposalViewManager, proposalSummaryViewManager) 
				        {
					        AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
				        };
             
				        proposalWaitHandle.Register(dispatcher);
				        proposalSummaryWaitHandle.Register(dispatcher);
		             
				        return dispatcher;
			        }))
		        );
	        
	        
	        // Dispatch 10 commands using the first command processor
	        CommandProcessingResult commandResult1 = null;
	        var numberOfCommands1 = 10;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        commandResult1 = commandProcessor.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    );
	        
	        await proposalWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalView = proposalViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalView);
	        Assert.AreEqual(
		        expected: 10,
		        actual: proposalView.Labels.Count
	        );
	        Assert.AreEqual(
		        commandResult1.TimeStamp,
		        proposalView.LastGlobalSequenceNumber
	        );
	        
	        await proposalSummaryWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
	        proposalSummaryView = proposalSummaryViewManager.Load("oAp1");
	        
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 10,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        commandResult1.TimeStamp,
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        commandProcessor.Dispose();
	        
	        //Do we need to reset these?
	        proposalWaitHandle = new ViewManagerWaitHandle();
	        proposalSummaryWaitHandle = new ViewManagerWaitHandle();
	        
	        proposalViewManager = new SpecialInMemoryViewManager<ProposalView>();
	        
	        commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events2"))
			        .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
				        var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
				        var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
				        var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
				        var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		             
				        var dispatcher = new SpecialViewManagerEventDispatcher(
					        repository,
					        eventStore,
					        serializer,
					        typeMapper,
					        proposalViewManager, proposalSummaryViewManager) 
				        {
					        AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
				        };
             
				        proposalWaitHandle.Register(dispatcher);
				        proposalSummaryWaitHandle.Register(dispatcher);
		             
				        return dispatcher;
			        }))
		        );

	        // Now dispatch using a different processor, but same view
	        CommandProcessingResult commandResult2 = null;
	        var numberOfCommands2 = 5;
	        Enumerable.Range(0, numberOfCommands2).ToList().ForEach(i =>
		        commandResult2 = commandProcessor.ProcessCommand(new ProposalCommand($"oBp1", $"Label-2 {i}"))
	        );
	        
	        await proposalWaitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
	        proposalView = proposalViewManager.Load("oBp1");
	        
	        Assert.NotNull(proposalView);
	        Assert.AreEqual(
		        expected: 5,
		        actual: proposalView.Labels.Count
	        );
	        Assert.AreEqual(
		        commandResult2.TimeStamp,
		        proposalView.LastGlobalSequenceNumber
	        );
	        
	        await proposalSummaryWaitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
	        
	        // The new AR is here
	        proposalSummaryView = proposalSummaryViewManager.Load("oBp1");
	        Assert.NotNull(proposalSummaryView);
	        Assert.AreEqual(
		        expected: 5,
		        actual: proposalSummaryView.LabelCount
	        );
	        Assert.AreEqual(
		        commandResult2.TimeStamp,
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
		        commandResult1.TimeStamp,
		        proposalSummaryView.LastGlobalSequenceNumber
	        );
	        
	        commandProcessor.Dispose();
        }
        
        // We're testing the idea that we have two different event streams : Events1 and Events2
        // but they share the view models: proposalView, proposalSummaryView. (in reality, proposalView
        // isn't shared - they're on different databases
        
        [Test]
        public async Task MongoDb_ThisIsHowItActuallyWorksInMercenta()
        {
	        FakeGlobalSequenceNumberService.Reset();
	        var proposalWaitHandle = new ViewManagerWaitHandle();
	        var proposalSummaryWaitHandle = new ViewManagerWaitHandle();
	        var proposalViewManager = new MongoDbViewManager<ProposalView>(_mongoDatabase);
	        var proposalSummaryViewManager = new MongoDbViewManager<ProposalSummaryView>(_mongoDatabase);
	        ProposalView proposalView = null;
	        ProposalSummaryView proposalSummaryView = null;
	        
	        //------------------------------------------------------------------------------------
	        // oA logs in and dispatches 10 events for p1 for their own event store "Events1"
	        //------------------------------------------------------------------------------------
	        
	        var commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events1"))
			        .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
				        var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
				        var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
				        var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
				        var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		             
				        var dispatcher = new ViewManagerEventDispatcher(
					        repository,
					        eventStore,
					        serializer,
					        typeMapper,
					        proposalViewManager, proposalSummaryViewManager) 
				        {
					        AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
				        };
             
				        proposalWaitHandle.Register(dispatcher);
				        proposalSummaryWaitHandle.Register(dispatcher);
		             
				        return dispatcher;
			        }))
		        );
	        
	        CommandProcessingResult commandResult1 = null;
	        var numberOfCommands1 = 10;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        commandResult1 = commandProcessor.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    );
	        
	        await proposalWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
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
	        
	        await proposalSummaryWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
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
	        
	        //Do we need to reset these?
	        proposalWaitHandle = new ViewManagerWaitHandle();
	        proposalSummaryWaitHandle = new ViewManagerWaitHandle();
	        
	        //------------------------------------------------------------------------------------
	        // oB logs in and dispatches 5 events for oBp1 for their own event store "Events2"
	        //------------------------------------------------------------------------------------
	        
	        proposalViewManager = new MongoDbViewManager<ProposalView>(_mongoDatabase);
	        commandProcessor = 
		        CreateCommandProcessor(config => config
			        .Logging(l => l.UseConsole(minLevel: Logger.Level.Debug))
			        .EventStore(e => e.UseMongoDb(_mongoDatabase, "Events2"))
			        .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
				        var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
				        var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
				        var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
				        var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		             
				        var dispatcher = new ViewManagerEventDispatcher(
					        repository,
					        eventStore,
					        serializer,
					        typeMapper,
					        proposalViewManager, proposalSummaryViewManager) 
				        {
					        AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
				        };
             
				        proposalWaitHandle.Register(dispatcher);
				        proposalSummaryWaitHandle.Register(dispatcher);
		             
				        return dispatcher;
			        }))
		        );

	        // Now dispatch using a different processor, but same view
	        CommandProcessingResult commandResult2 = null;
	        var numberOfCommands2 = 5;
	        Enumerable.Range(0, numberOfCommands2).ToList().ForEach(i =>
		        commandResult2 = commandProcessor.ProcessCommand(new ProposalCommand($"oBp1", $"Label-2 {i}"))
	        );
	        
	        await proposalWaitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
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
	        
	        await proposalSummaryWaitHandle.WaitForAll(commandResult2, TimeSpan.FromSeconds(30));
	        
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
			        .EventDispatcher(e => e.Register<IEventDispatcher>(r => {
				        var repository = (IAggregateRootRepository)r.GetService(typeof(IAggregateRootRepository));
				        var serializer = (IDomainEventSerializer)r.GetService(typeof(IDomainEventSerializer));
				        var typeMapper = (IDomainTypeNameMapper)r.GetService(typeof(IDomainTypeNameMapper));
				        var eventStore = (IEventStore)r.GetService(typeof(IEventStore));
		             
				        var dispatcher = new ViewManagerEventDispatcher(
					        repository,
					        eventStore,
					        serializer,
					        typeMapper,
					        proposalViewManager, proposalSummaryViewManager) 
				        {
					        AutomaticCatchUpInterval = TimeSpan.FromHours(24), //<effectively disable automatic catchup
				        };
             
				        proposalWaitHandle.Register(dispatcher);
				        proposalSummaryWaitHandle.Register(dispatcher);
		             
				        return dispatcher;
			        }))
		        );
	        
	        numberOfCommands1 = 20;
	        Enumerable.Range(0, numberOfCommands1).ToList().ForEach(i =>
		        commandResult1 = commandProcessor.ProcessCommand(new ProposalCommand($"oAp1", $"Label-1 {i}"))
		    );
	        
	        await proposalWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
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
	        
	        await proposalSummaryWaitHandle.WaitForAll(commandResult1, TimeSpan.FromSeconds(30));
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