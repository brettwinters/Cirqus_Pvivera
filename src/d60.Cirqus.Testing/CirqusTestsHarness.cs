﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using d60.Cirqus.Commands;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Identity;
using EnergyProjects.Tests.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Testing
{
    [DebuggerStepThrough]
    public abstract class CirqusTestsHarness
    {
        protected const string checkmark = "\u221A";
        protected const string cross = "\u2717";
        private Stack<Tuple<Type, string>> ids;
        private TextFormatter formatter;
        private List<long> arrangedEvents;
        private IEnumerable<DomainEvent> results;

        protected TestContext Context { get; private set; }
        protected Action<DomainEvent> BeforeEmit = x => { };
        protected Action<DomainEvent> AfterEmit = x => { };
        protected Action<Command> BeforeExecute = x => { };

        protected void Begin(IWriter writer)
        {
            ids = new Stack<Tuple<Type, string>>();
            arrangedEvents = new List<long>();
            results = null;

            formatter = new TextFormatter(writer);

            Configure();
        }


        protected void End(bool isInExceptionalState)
        {
            // only if we are _not_ in an exceptional state
            if (!isInExceptionalState)
            {
                AssertAllEventsExpected();
            }
        }

        protected void Configure(
            Action<IOptionalConfiguration<TestContext>> configurator = null)
        {
            var services = new ServiceCollection();
            services.AddTestContext(configurator);

            var provider = services.BuildServiceProvider();
            Context = provider.GetService<TestContext>();
        }

        protected abstract void Fail();


        #region Given


        protected void Given(params ExecutableCommand[] commands)
        {
            foreach (var command in commands)
            {
                Context.ProcessCommand(command);

                //formatter
                //    .Block("Given that:")
                //    .Write(command, new EventFormatter(formatter))
                //    .NewLine()
                //    .NewLine();
            }
        }

        protected void Emit<T>(params DomainEvent[] events) where T : class => Emit(Latest<T>(), events);

        protected void Emit<T>(Id<T> id, params DomainEvent[] events) => Emit<T>((string)id, events);

        protected void Emit<T>(string id, params DomainEvent[] events)
        {
            foreach (var @event in events)
            {
                Emit<T>(id, @event);
            }
        }


        private void Emit<T>(string id, DomainEvent @event)
        {
            @event.Meta[DomainEvent.MetadataKeys.AggregateRootId] = id;

            BeforeEmit(@event);

            TryRegisterId<T>(id);

            Context.Save(typeof(T), @event);

            arrangedEvents.Add(@event.GetGlobalSequenceNumber());

            AfterEmit(@event);

            formatter
                .Block("Given that:")
                .Write(@event, new EventFormatter(formatter))
                .NewLine()
                .NewLine();
        }
        #endregion


        protected void When(ExecutableCommand command)
        {
            BeforeExecute(command);

            formatter
                .Block("When users:")
                .Write(command, new EventFormatter(formatter));

            results = Context.ProcessCommand(command);
        }

        #region Throws

        // ReSharper disable once InconsistentNaming

        protected void Throws<T>(ExecutableCommand When) where T : Exception
        {
            Exception exceptionThrown = null;
            try
            {
                this.When(When);
            }
            catch (Exception e)
            {
                exceptionThrown = e;
            }

            formatter.Block("Then:");

            Assert(
                exceptionThrown is T,
                () => formatter.Write("It throws " + typeof(T).Name).NewLine(),
                () =>
                {
                    if (exceptionThrown == null)
                    {
                        formatter.Write("But it did not.");
                        return;
                    }

                    formatter.Write("But got " + exceptionThrown.GetType().Name).NewLine();
                });


            // consume all events
            results = Enumerable.Empty<DomainEvent>();
        }

        // ReSharper disable once InconsistentNaming

        protected void Throws<T>(string message, ExecutableCommand When) where T : Exception
        {
            Exception exceptionThrown = null;

            try
            {
                this.When(When);
            }
            catch (Exception e)
            {
                exceptionThrown = e;
            }

            formatter.Block("Then:");

            Assert(
                exceptionThrown is T && exceptionThrown.Message == message,
                () => formatter
                    .Write("It throws " + typeof(T).Name).NewLine()
                    .Indent().Write("Message: \"" + message + "\"").Unindent()
                    .NewLine(),
                () =>
                {
                    if (exceptionThrown == null)
                    {
                        formatter.Write("But it did not.");
                        return;
                    }

                    formatter.Write("But got " + exceptionThrown.GetType().Name).NewLine()
                        .Indent().Write("Message: \"" + exceptionThrown.Message + "\"").Unindent();
                });

            // consume all events
            results = Enumerable.Empty<DomainEvent>();
        }
        #endregion


        protected void Then<T>() where T : DomainEvent
        {
            if (results == null)
            {
                results = Context.History.Where(x => !arrangedEvents.Contains(x.GetGlobalSequenceNumber()));
            }

            var next = results.FirstOrDefault();

            formatter.Block("Then:");

            Assert(
                next is T,
                () => formatter.Write(typeof(T).Name).NewLine(),
                () => formatter.Write("But we got " + next.GetType().Name).NewLine());

            // consume one event
            results = results.Skip(1);
        }


        protected void Then<T>(params DomainEvent[] events) where T : class => Then(Latest<T>(), events);


        protected void Then<T>(Id<T> id, params DomainEvent[] events) => Then((string)id, events);


        protected void Then(string id, params DomainEvent[] events)
        {
            if (events.Length == 0) return;

            formatter.Block("Then:");

            foreach (var expected in events)
            {
                if (results == null)
                {
                    results = Context.History.Where(x => !arrangedEvents.Contains(x.GetGlobalSequenceNumber()));
                }

                var actual = results.FirstOrDefault();

                if (actual == null)
                {
                    Assert(false,
                        () => formatter.Write(expected, new EventFormatter(formatter)).NewLine(),
                        () => formatter.Block("But we got nothing."));

                    return;
                }

                expected.Meta[DomainEvent.MetadataKeys.AggregateRootId] = id;

                var jActual = Context.EventSerializer.Serialize(actual);
                var jExpected = Context.EventSerializer.Serialize(expected);

                Assert(
                    actual.GetAggregateRootId().Equals(id) &&
                    actual.GetType() == expected.GetType() &&
                    jActual.Data.SequenceEqual(jExpected.Data),
                    () => formatter.Write(expected, new EventFormatter(formatter)).NewLine(),
                    () =>
                    {
                        formatter.Block("But we got this:")
                            .Indent().Write(actual, new EventFormatter(formatter)).Unindent()
                            .EndBlock();

                        if (!jActual.IsJson() || !jExpected.IsJson()) return;

                        var differ = new Differ();
                        var diffs = differ.LineByLine(
                            Encoding.UTF8.GetString(jActual.Data),
                            Encoding.UTF8.GetString(jExpected.Data));

                        var diff = differ.PrettyLineByLine(diffs);

                        formatter
                            .NewLine().NewLine()
                            .Write("Diff:").NewLine()
                            .Write(diff).NewLine();
                    });

                // consume events
                results = results.Skip(1);
            }
        }

        #region Brett's CHanges - might be better to override the base

        /// <summary>
        /// Only matches the properties that are provided, otherwise just compares the default values. At the moment the writer
        /// does not ignore. We should adjust the writer later
        /// </summary>

        protected void Then(string id, bool exactMatch, params DomainEvent[] events) => ThenAssert(id, exactMatch, events);

        /// <summary>
        /// Only matches the properties that are provided, otherwise just compares the default values. At the moment the writer
        /// does not ignore. We should adjust the writer later
        /// </summary>

        protected void Then<T>(bool exactMatch, params DomainEvent[] events) where T : class => ThenAssert(Latest<T>(), exactMatch, events);


        private void ThenAssert(string id, bool matchExact = true, params DomainEvent[] events)
        {
            if (events.Length == 0) return;

            formatter.Block("Then:");

            foreach (var expected in events)
            {
                if (results == null)
                {
                    results = Context.History.Where(x => !arrangedEvents.Contains(x.GetGlobalSequenceNumber()));
                }

                var actual = results.FirstOrDefault();

                if (actual == null)
                {
                    Assert(false,
                        () => formatter.Write(expected, new EventFormatter(formatter)).NewLine(),
                        () => formatter.Block("But we got nothing."));

                    return;
                }

                expected.Meta[DomainEvent.MetadataKeys.AggregateRootId] = id;

                // hack to workaround an ignore functionality since i have no idea how the SequenceEqual of the 
                // binary data works
                //var ignoredValues = Enumerable.Empty<string>();
                if (!matchExact)
                {
                    var ignoredValues = OverwriteExpectedWithActualValueIfNotSpecified();
                    if (ignoredValues.Any())
                    {
                        formatter.Indent().Block($"Ignored : {string.Join(", ", ignoredValues)}").Unindent();
                    }
                }

                var jActual = Context.EventSerializer.Serialize(actual);
                var jExpected = Context.EventSerializer.Serialize(expected);

                Assert(
                    condition: actual.GetAggregateRootId().Equals(id) && actual.GetType() == expected.GetType() && jActual.Data.SequenceEqual(jExpected.Data),
                    writeExpected: () => formatter.Write(obj: expected, formatter: new EventFormatter(formatter)).NewLine(),
                    onFail: () =>
                    {
                        formatter.Block("But we got this:")
                            .Indent().Write(obj: actual, formatter: new EventFormatter(formatter)).Unindent()
                            .EndBlock();

                        if (!jActual.IsJson() || !jExpected.IsJson()) return;

                        var differ = new Differ();
                        var diffs = differ.LineByLine(
                            text1: Encoding.UTF8.GetString(jActual.Data),
                            text2: Encoding.UTF8.GetString(jExpected.Data));

                        var diff = differ.PrettyLineByLine(diffs);

                        formatter
                            .NewLine().NewLine()
                            .Write("Diff:").NewLine()
                            .Write(diff).NewLine();
                    }
                );

                // consume events
                results = results.Skip(1);
                

                #region
                
                // When the expected is not specified, then it's equal to the default value.
                // and then its not specified, we set the expected equal to the actual value
                // (as though it was set)
                IEnumerable<string> OverwriteExpectedWithActualValueIfNotSpecified()
                {
                    var ignored = new List<string>();
                    foreach (var expectedProperty in expected.GetType().GetProperties())
                    {
                        if (IsExpectedNotSetByUser())
                        {
                            var actualProperty = TypeDescriptor.GetProperties(actual)[expectedProperty.Name];

                            // If the actual property is not found in the expected object or we can't
                            // set the actual property value then just skip
                            if (actualProperty != null)
                            {
	                            try
	                            {
		                            actualProperty.SetValue(expected, actualProperty.GetValue(actual));
		                            ignored.Add(expectedProperty.Name);
	                            }
	                            catch
	                            {
		                           //ignore
	                            }
	                             
                            }
                           
                        }
                        
                        #region

                        // The expected value is not set by the user when its value equals the default value
                        bool IsExpectedNotSetByUser()
                        {
                            var defaultValue = GetDefault(expectedProperty.PropertyType);
                            var actualValue = expectedProperty.GetValue(expected);

                            // we need to handle strings a bit differently because default(string) == null, but 
                            // the Event might have a non-nullable string (in this case default == "")
                            if (expectedProperty.PropertyType == typeof(string))
                            {
                                if (string.IsNullOrEmpty(defaultValue?.ToString()) && string.IsNullOrEmpty(actualValue?.ToString()))
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                if (object.Equals(actualValue, defaultValue))
                                {
                                    return true;
                                }
                            }

                            return false;
                        }
                        
                        #endregion
                    }

                    return ignored;
                }

                #endregion
            }
        }

        private static T GetDefault<T>() => (T)GetDefault(typeof(T));




        private static object GetDefault(Type type)
        {




            // If no Type was supplied, if the Type was a reference type, or if the Type was a System.Void, return null
            if (type == null || !type.IsValueType || type == typeof(void))
                return null;

            // If the supplied Type has generic parameters, its default value cannot be determined
            if (type.ContainsGenericParameters)
                throw new ArgumentException(
                    "{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type +
                    "> contains generic parameters, so the default value cannot be retrieved");

            // If the Type is a primitive type, or if it is another publicly-visible value type (i.e. struct), return a 
            //  default instance of the value type
            if (type.IsPrimitive || !type.IsNotPublic)
            {
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        "{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe Activator.CreateInstance method could not " +
                        "create a default instance of the supplied value type <" + type +
                        "> (Inner Exception message: \"" + e.Message + "\")", e);
                }
            }

            // Fail with exception
            throw new ArgumentException("{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type +
                "> is not a publicly-visible type, so the default value cannot be retrieved");
        }


        #endregion

        protected void ThenNothing() => ThenNo<DomainEvent>();


        protected void ThenNo<T>() where T : DomainEvent
        {
            formatter.Block("Then:");

            var eventsOfType = results.OfType<T>().ToList();

            Assert(
                !eventsOfType.Any(),
                () => formatter.Write(string.Format("No {0} is emitted", typeof(T).Name)),
                () =>
                {
                    formatter.Block("But we got this:");
                    foreach (var @event in eventsOfType)
                    {
                        formatter.Write(@event, new EventFormatter(formatter)).NewLine();
                    }
                    formatter.EndBlock();
                });

            // consume all events
            results = Enumerable.Empty<DomainEvent>();
        }

        protected Id<T> NewId<T>(params object[] args) where T : class
        {
            var id = GenerateId<T>(args);
            TryRegisterId<T>(id);
            return id;
        }

        protected Id<T> Id<T>() where T : class => Id<T>(1);

        protected Id<T> Id<T>(int index) where T : class
        {
            var array = ids.Where(x => x.Item1 == typeof(T)).Reverse().ToArray();
            if (array.Length < index)
            {
                throw new IndexOutOfRangeException(string.Format("Could not find Id<{0}> with index {1}", typeof(T).Name, index));
            }

            return Identity.Id<T>.Parse(array[index - 1].Item2);
        }

        protected Id<T> Latest<T>() where T : class
        {
            if (!TryGetLatest(out Id<T> id))
            {
                throw new InvalidOperationException(string.Format("Can not get latest {0} id, since none exists.", typeof(T).Name));
            }

            return id;
        }

        protected bool TryGetLatest<T>(out Id<T> latest) where T : class
        {
            var lastestOfType = ids.FirstOrDefault(x => x.Item1 == typeof(T));

            if (lastestOfType == null)
            {
                latest = default(Id<T>);
                return false;
            }

            latest = Identity.Id<T>.Parse(lastestOfType.Item2);
            return true;
        }

        protected virtual Id<T> GenerateId<T>(params object[] args) where T : class => Identity.Id<T>.New(args);

        private void TryRegisterId<T>(string id)
        {
            var candidate = ids.SingleOrDefault(x => x.Item1.IsAssignableFrom(typeof(T)) && x.Item2 == id);

            var newId = Tuple.Create(typeof(T), id);

            if (candidate == null)
            {
                ids.Push(newId);
                return;
            }

            if (!newId.Item1.IsAssignableFrom(candidate.Item1))
            {
                throw new InvalidOperationException(string.Format(
                    "You tried to register a new id '{0}' for type '{1}', but the id already exist and is for non-compatible type '{2}'",
                    id, newId.Item1, candidate.Item1));
            }
        }

        private void AssertAllEventsExpected()
        {
            if (results != null && results.Any())
            {
                Assert(false, () => formatter.Write("Expects no more events").NewLine(), () =>
                {
                    formatter.Write("But found:").NewLine().Indent();
                    foreach (var @event in results)
                    {
                        formatter.Write(@event, new EventFormatter(formatter));
                    }
                    formatter.Unindent();
                });
            }
        }

        [DebuggerHidden]
        private void Assert(bool condition, Action writeExpected, Action onFail)
        {
            if (condition)
            {
                formatter.Write(checkmark + " ").Indent();
                writeExpected();
                formatter.Unindent().NewLine();
            }
            else
            {
                formatter.Write(cross + " ").Indent();
                writeExpected();
                formatter.Unindent().NewLine();

                onFail();

                Fail();
            }
        }
    }
}
