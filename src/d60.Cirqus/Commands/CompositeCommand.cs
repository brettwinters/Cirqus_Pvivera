﻿using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Aggregates;

namespace d60.Cirqus.Commands;

/// <summary>
/// Composite command that allows for packing up multiple commands and have them executed in one single unit of work
/// </summary>
public class CompositeCommand<TAggregateRoot> : Command<TAggregateRoot> where TAggregateRoot : AggregateRoot, new()
{
	public List<Command<TAggregateRoot>> Commands { get; set; }

	public CompositeCommand(params Command<TAggregateRoot>[] commands)
		: base(commands.First().AggregateRootId)
	{
		var addressedAggregateRoots = commands.Select(c => c.AggregateRootId).Distinct().ToList();

		if (addressedAggregateRoots.Count > 1)
		{
			throw new ArgumentException(
				$"Cannot address more than one single aggregate root instance with a composite command - the following aggregate root IDs were addressed: {string.Join(", ", addressedAggregateRoots)}"
			);
		}

		Commands = commands.ToList();
	}

	public override void Execute(TAggregateRoot aggregateRoot)
	{
		foreach (var command in Commands)
		{
			command.Execute(aggregateRoot);
		}
	}
}

/// <summary>
/// Composite command that allows for packing up multiple commands and have them executed in one single unit of work
/// </summary>
public class CompositeExecutableCommand : ExecutableCommand
{
	public List<ExecutableCommand> Commands { get; set; }

	public CompositeExecutableCommand(params ExecutableCommand[] commands) => Commands = commands.ToList();

	public override void Execute(ICommandContext context) => Commands.ForEach(c => c.Execute(context));
}

//public class CompositeExecutableCommand<TAggregateRoot> : ExecutableCommand where TAggregateRoot : AggregateRoot, new()
//{
//    public List<ExecutableCommand> Commands { get; set; }

//    public CompositeExecutableCommand(params ExecutableCommand[] commands) {
//        Commands = commands.ToList();
//    }

//    public override void Execute(ICommandContext context) {
//        foreach (var command in Commands) {
//            switch (command) {
//                case Command<TAggregateRoot> aggregateCommand:
//                    aggregateCommand.Execute(context.Load<TAggregateRoot>(aggregateCommand.AggregateRootId));
//                    break;
//                case ExecutableCommand executable:
//                    executable.Execute(context);
//                    break;
//            }
//        }
//    }
//}

public class CompositeCommand
{
	public static CompositeCommandBuilder<TAggregateRoot> For<TAggregateRoot>()
		where TAggregateRoot : AggregateRoot, new()
	{
		return new CompositeCommandBuilder<TAggregateRoot>();
	}
}

public class CompositeCommandBuilder<TAggregateRoot> where TAggregateRoot : AggregateRoot, new()
{
	readonly List<Command<TAggregateRoot>> _commands = new List<Command<TAggregateRoot>>();

	public CompositeCommandBuilder<TAggregateRoot> With(Command<TAggregateRoot> command)
	{
		_commands.Add(command);
		return this;
	}

	public static implicit operator CompositeCommand<TAggregateRoot>(CompositeCommandBuilder<TAggregateRoot> builder)
	{
		return new CompositeCommand<TAggregateRoot>(builder._commands.ToArray());
	}
}

public class CompositeExecutableCommandBuilder
{
	private readonly List<ExecutableCommand> _commands = new List<ExecutableCommand>();
	public CompositeExecutableCommandBuilder With(ExecutableCommand command) {
		_commands.Add(command);
		return this;
	}
	public static implicit operator ExecutableCommand(CompositeExecutableCommandBuilder builder) => new CompositeExecutableCommand(builder._commands.ToArray());
}