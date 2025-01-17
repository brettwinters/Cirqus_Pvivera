﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using d60.Cirqus.Events;

namespace d60.Cirqus.Views.ViewManagers.Locators;

/// <summary>
/// Special view locator that allows for using a "handler-like" dispatch mechanism to map events to view IDs.
/// Implement one or more <see cref="IGetViewIdsFor{TDomainEvent}"/> interfaces, closing it with either concrete
/// event types or base types (or interfaces, even) in order to have events of those types dispatched to the view locator.
/// </summary>
public class HandlerViewLocator : ViewLocator
{
	readonly ConcurrentDictionary<Type, MethodInfo> _handlerMethodsByDomainEventType = new();

	protected override IEnumerable<string> GetViewIds(
		IViewContext context, 
		DomainEvent e)
	{
		var handlerMethod = _handlerMethodsByDomainEventType.GetOrAdd(e.GetType(), GetHandlerMethodFor);

		if (handlerMethod == null)
		{
			return Array.Empty<string>();
		}

		try
		{
			var ids = ((IEnumerable<string>)handlerMethod.Invoke(this, new object[] { context, e })).ToList();

			return ids;
		}
		catch (Exception exception)
		{
			throw new ApplicationException($"Could not get IDs for domain event {e}!", exception);
		}
	}

	MethodInfo GetHandlerMethodFor(
		Type domainEventType)
	{
		var typesToCheck = new[] { domainEventType }
			.Concat(domainEventType.GetInterfaces())
			.Concat(GetBaseTypes(domainEventType));

		return typesToCheck
			.Select(typeToCheck => new[] { typeof(IViewContext), typeToCheck })
			.Select(parameterTypes => GetType().GetMethod("GetViewIds", parameterTypes))
			.FirstOrDefault(method => method != null);
	}

	IEnumerable<Type> GetBaseTypes(Type type)
	{
		return type.BaseType == null
			? Type.EmptyTypes
			: new[] { type.BaseType }.Concat(GetBaseTypes(type.BaseType));
	}
}