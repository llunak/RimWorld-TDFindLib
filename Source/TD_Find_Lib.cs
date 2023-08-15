﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public class Mod : Verse.Mod
	{
		public static Settings settings;
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings, wait for defs and anything to load.
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				settings = GetSettings<Settings>();

				if (settings.firstUse)
				{
					List<SearchGroup> groups = DefaultSearches.CopyLibrary;

					if (groups != null)
					{
						settings.firstUse = false;
						foreach(var group in groups)
							settings.Add(group);
						settings.Write();
					}
				}

				if (!settings.warnedAnyNull)
				{
					Verse.Log.Error("TD Find Lib here: The definitions for Any/None filters selections have changed around a bit. Double-check if any of your filters have those set."); //also to stop hugslibs from autostarting
					
					settings.warnedAnyNull = true;
					settings.Write();

					LongEventHandler.QueueLongEvent(() =>
						Find.WindowStack.Add(new Dialog_MessageBox(@"TD Find Lib here: The definitions for Any/None filters selections have changed around a bit.

Double-check if any of your filters have those set.")),
						"TDFINDLIB", true, _ => { });
				}
			});
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "TD.TDFindLibrary".Translate();
		}
	}
}