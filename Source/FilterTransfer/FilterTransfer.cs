﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	// The holder of the receivers and providers
	public static class FilterTransfer
	{
		public static List<IFilterReceiver> receivers = new();
		public static List<IFilterProvider> providers = new();

		public static void Register(object obj)
		{
			if (obj is IFilterReceiver receiver)
				receivers.Add(receiver);
			if (obj is IFilterProvider provider)
				providers.Add(provider);
		}
		public static void Deregister(object obj)
		{
			receivers.Remove(obj as IFilterReceiver);
			providers.Remove(obj as IFilterProvider);
		}
	}


	// You can export  to  a receiver
	// You can import from a provider
	public interface IFilterReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public void Receive(FindDescription desc);
	}

	public interface IFilterProvider
	{
		public enum Method { None, Single, Selection, Grouping }

		public string Source { get; }
		public string ProvideName { get; }
		public Method ProvideMethod();

		public FindDescription ProvideSingle();
		public List<FindDescription> ProvideSelection();
		public List<FilterGroup> ProvideGrouping();
	}

	//public interface IFilterGroupReceiver
	//public interface IFilterGroupProvider


	[StaticConstructorOnStartup]
	public class ClipboardTransfer : IFilterReceiver, IFilterProvider
	{
		static ClipboardTransfer()
		{
			FilterTransfer.Register(new ClipboardTransfer());
		}


		public string Source => null;	//always used

		public string ReceiveName => "Copy to clipboard";
		public string ProvideName => "Paste from clipboard";

		public void Receive(FindDescription desc)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(desc.CloneForSave());
		}



		public IFilterProvider.Method ProvideMethod()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.IsValid<FindDescription>(clipboard) ? IFilterProvider.Method.Single
				: ScribeXmlFromString.IsValid<FilterGroup>(clipboard) ? IFilterProvider.Method.Selection
				: IFilterProvider.Method.None;
		}

		public FindDescription ProvideSingle()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<FindDescription>(clipboard);
		}

		public List<FindDescription> ProvideSelection()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<FilterGroup>(clipboard, null, null);
		}

		public List<FilterGroup> ProvideGrouping() => null;
	}
}
