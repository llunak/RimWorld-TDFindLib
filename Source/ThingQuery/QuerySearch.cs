﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	// QuerySearch is the class to do a TDFindLib Thing Query Search, 
	// There's a few parts:
	// - The SearchListType narrows the basic type of thing you're searching
	// - The ThingQueries (the bulk of the lib) are countless options
	//    to select from every detail about a thing.
	// - And then, which map/maps to run the search on.


	// SearchListType:
	// What basic type of thing are you searching.
	[Flags]
	public enum SearchListType
	{
		Selectable = 1,	// Known as "Map", requires processing on All things.

		// Direct references to listerThings lists
		Everyone = 2,
		Items = 4,
		Buildings = 8,
		Plants = 0x10,

		Natural = 0x20,	// Extra processing required
		Junk = 0x40, // "Haulable" things that are not "Items"
		All = 0x80,  // Very long including every blade of grass
		Inventory = 0x100,	// Actually fast since there aren't many ThingHolders on a map

		//devmode options
		Haulables = 0x200,
		Mergables = 0x400,
		FilthInHomeArea = 0x800
	}

	// SearchMapType:
	// What map or maps you're searching on.
	// For ChosenMaps, SearchParameters.searchMaps is set by the user
	// For CurMap / AllMaps, SearchParameters.searchMaps is null, but SearchResult.resultMaps is set when a search is run
	// TODO search filter on colony/raid maps.
	public enum SearchMapType	{ CurMap, AllMaps, ChosenMaps}
	public class SearchParameters
	{
		// What basic list to search (TODO: list of types.)
		public SearchListType listType = SearchListType.Selectable;

		// How to look
		public bool matchAllQueries = true;

		// Where to look
		public SearchMapType mapType; //default is CurMap
		public List<Map> searchMaps = new();

		public SearchParameters Clone(bool includeMaps = true)
		{
			SearchParameters result = new();

			result.listType = listType;
			result.matchAllQueries = matchAllQueries;
			result.mapType = mapType;

			if(includeMaps)
				result.searchMaps.AddRange(searchMaps);
			
			return result;
		}
		public void ExposeData()
		{
			Scribe_Values.Look(ref listType, "listType");
			Scribe_Values.Look(ref matchAllQueries, "matchAllQueries", true);
			Scribe_Values.Look(ref mapType, "mapType");

			Scribe_Collections.Look(ref searchMaps, "searchMaps", LookMode.Reference);
		}
	}

	// What was found, and from where.
	public class SearchResult
	{
		public List<Map> resultMaps = new();

		public List<Thing> allThings = new();
		public Dictionary<Map, List<Thing>> mapThings = new();
		//Todo things by def/map?

		public bool godMode;
	}

	// The QuerySearch is the root of a TDFindLib search
	// - SearchListType which narrows what that things to look at
	// - owner of a set of queries
	// - What maps to search on
	// - Performs the search
	// - Holds the list of found things.
	public class QuerySearch : IExposable, IQueryHolder
	{
		public string name = "??NAME??";

		// Basic query settings:
		private SearchParameters parameters = new();
		// What to search for
		public QueryHolder children;
		// Resulting things
		public SearchResult result = new();


		// "Inactive" is for the saved library of searches to Clone from.
		// inactive won't actually fill their lists,
		// which normally happens whenever queries are edited
		public bool active;

		// If you clone a QuerySearch it starts unchanged.
		// Not used directly but good to know if a save is needed.
		public bool changed;


		// from IQueryHolder:
		public QuerySearch RootQuerySearch => this;
		public QueryHolder Children => children;



		// Parameter accessors:
		public SearchListType ListType => parameters.listType;

		public void SetListType(SearchListType newType, bool remake = true)
		{
			parameters.listType = newType;

			FixListType();

			if (remake)	RemakeList();
		}

		public void AddListType(SearchListType newType, bool remake = true)
		{
			parameters.listType |= newType;

			//Set off "all" if selected another type
			if ((newType & (SearchListType.Everyone | SearchListType.Items | SearchListType.Buildings
					| SearchListType.Plants | SearchListType.Natural | SearchListType.Junk)) != 0)
			{
				parameters.listType &= ~(SearchListType.Selectable | SearchListType.All);
			}

			FixListType();

			if (remake) RemakeList();
		}

		public void RemoveListType(SearchListType oldType, bool remake = true)
		{
			parameters.listType &= ~oldType;

			FixListType();

			if (remake) RemakeList();
		}

		public void ToggleListType(SearchListType toggleType, bool remake = true)
		{
			//sorry ^= but there's more processing to do
			if (parameters.listType.HasFlag(toggleType))
				RemoveListType(toggleType);
			else
				AddListType(toggleType);
		}
		// Make sure the list type doesn't have duplicate ListerThing types
		private void FixListType()
		{
			if ((parameters.listType & (SearchListType.Selectable | SearchListType.All)) != 0)
			{
				parameters.listType &= ~(SearchListType.Everyone | SearchListType.Items | SearchListType.Buildings
					| SearchListType.Plants | SearchListType.Natural | SearchListType.Junk);
			}
			if ((parameters.listType & SearchListType.All) != 0)
			{
				parameters.listType &= ~(SearchListType.Selectable);
			}
			if (parameters.listType == 0)
				parameters.listType = SearchListType.Selectable;
		}


		// All Or Any Query
		public bool MatchAllQueries
		{
			get => parameters.matchAllQueries;
			set
			{
				parameters.matchAllQueries = value;

				RemakeList();
			}
		}



		// Map shenanigans
		public void SetSearchChosenMaps()
		{
			//Pretty much for inactive queries right?
			parameters.mapType = SearchMapType.ChosenMaps;
			parameters.searchMaps.Clear();
		}

		public void SetSearchMap(Map newMap, bool remake = true)
		{
			parameters.mapType = SearchMapType.ChosenMaps;
			parameters.searchMaps.Clear();
			parameters.searchMaps.Add(newMap);

			if (remake) RemakeList();
		}

		public void SetSearchMaps(IEnumerable<Map> newMaps, bool remake = true)
		{
			parameters.mapType = SearchMapType.ChosenMaps;
			parameters.searchMaps.Clear();
			parameters.searchMaps.AddRange(newMaps);

			if (remake) RemakeList();
		}

		public void AddSearchMap(Map newMap, bool remake = true)
		{
			parameters.mapType = SearchMapType.ChosenMaps;
			parameters.searchMaps.Add(newMap);

			if (remake) RemakeList();
		}

		public void RemoveSearchMap(Map oldMap, bool remake = true)
		{
			if (parameters.mapType != SearchMapType.ChosenMaps) return; //Huh?

			parameters.searchMaps.Remove(oldMap);

			if (remake) RemakeList();
		}

		public void ToggleSearchMap(Map toggleMap, bool remake = true)
		{
			if (parameters.mapType != SearchMapType.ChosenMaps)
			{
				SetSearchMap(toggleMap, remake);
				return;
			}

			if (parameters.searchMaps.Contains(toggleMap))
			{
				if (parameters.searchMaps.Count == 1)
					Messages.Message("Hey man we have to search somewhere", MessageTypeDefOf.RejectInput, false);
				else
					parameters.searchMaps.Remove(toggleMap);
			}
			else
				parameters.searchMaps.Add(toggleMap);

			if (remake) RemakeList();
		}

		public void SetSearchCurrentMap(bool remake = true)
		{
			if (parameters.mapType == SearchMapType.CurMap) return;

			parameters.mapType = SearchMapType.CurMap;
			parameters.searchMaps.Clear();

			if (remake) RemakeList();
		}

		public void SetSearchAllMaps(bool remake = true)
		{
			if (parameters.mapType == SearchMapType.AllMaps) return;

			parameters.mapType = SearchMapType.AllMaps;
			parameters.searchMaps.Clear();

			if (remake) RemakeList();
		}

		// Get maps shenanigans
		public SearchMapType MapType => parameters.mapType;

		public List<Map> ChosenMaps =>
			parameters.mapType == SearchMapType.ChosenMaps && !ForceCurMap() ? parameters.searchMaps : null;

		// Certain queries only work on the current map, so the entire tree will only work on the current map
		public bool AllMaps() =>
			parameters.mapType == SearchMapType.AllMaps && !ForceCurMap();

		public bool CurMap() =>
			parameters.mapType == SearchMapType.CurMap || ForceCurMap();

		public bool ForceCurMap() => Children.Any(f => f.CurMapOnly);



		// A new QuerySearch, inactive, "current map"
		public QuerySearch()
		{
			children = new(this);
		}

		// A new QuerySearch, active, with this map
		// (Or just calls base constructor when null)
		public QuerySearch(Map map = null) : this()
		{
			if (map != null)
			{
				active = true;
				SetSearchMap(map, false);
			}
		}


		public void Reset()
		{
			changed = true;

			parameters = new();
			children.Clear();
			result = new();
		}


		// This is a roundabout way to hijack the esc-keypress from a window before it closes the window.
		// Any window displaying this has to override OnCancelKeyPressed and call this
		public bool OnCancelKeyPressed()
		{
			return children.Any(f => f.OnCancelKeyPressed());
		}


		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref active, "active");
			parameters.ExposeData();

			Children.ExposeData();
		}


		public string GetMapNameSuffix()
		{
			StringBuilder sb = new(" <i>(");

			if (AllMaps())
				sb.Append("TD.AllMaps".Translate());
			else if (result.resultMaps.Count > 0)
				sb.Append(string.Join(", ", result.resultMaps.Select(m => m.Parent.LabelCap)));
			else if (parameters.searchMaps.Count > 0)
				sb.Append(string.Join(", ", parameters.searchMaps.Select(m => m.Parent.LabelCap)));
			else return "";

			//Don't write "Current Map", doesn't look good. It is "unknown" until searched anyway

			sb.Append(")</i>");

			return sb.ToString();
		}


		public string GetMapOptionLabel()
		{
			StringBuilder sb = new("Searching: ");

			if (AllMaps())
				sb.Append("TD.AllMaps".Translate());
			else if (CurMap())
				sb.Append("TD.CurrentMap".Translate());
			else if (parameters.searchMaps.Count == 1)
				sb.Append(parameters.searchMaps[0].Parent.LabelCap);
			else
				sb.Append("TD.ChosenMaps".Translate());

			return sb.ToString();
		}



		// Cloning shenanigans
		public enum CloneType { Save, Edit, Use }//Reference? Copy?

		//default(CloneArgs) CloneArgs is CloneType.Save
		public struct CloneArgs
		{
			public CloneType type;
			public Map map;
			public List<Map> maps;
			public string newName;

			public static CloneArgs save = new CloneArgs();
			public static CloneArgs edit = new CloneArgs() { type = CloneType.Edit };
			public static CloneArgs use = new CloneArgs() { type = CloneType.Use };
		}
		public QuerySearch Clone(CloneArgs args)
		{
			return args.type switch
			{
				CloneType.Save => CloneInactive(args.newName),

				CloneType.Edit => CloneInactive(args.newName),

				CloneType.Use =>
				args.maps != null ? CloneForUse(args.maps, args.newName)
				: CloneForUseSingle(args.map, args.newName),

				_ => null
			};
		}

		public QuerySearch CloneInactive(string newName = null)
		{
			QuerySearch newSearch = new QuerySearch()
			{
				name = newName ?? name,
				active = false,
				parameters = parameters.Clone(false)
			};

			newSearch.children = children.Clone(newSearch);

			return newSearch;
		}
		public QuerySearch CloneForUseSingle(Map newMap = null, string newName = null)
		{
			if (newMap != null)
				return CloneForUse(new List<Map> { newMap }, newName);
			else
				return CloneForUse(null, newName);
		}

		public QuerySearch CloneForUse(List<Map> newMaps = null, string newName = null)
		{
			QuerySearch newSearch = new QuerySearch()
			{
				name = newName ?? name,
				active = true,
				parameters = parameters.Clone()
			};


			// If you ask for a map, you're changing the setting.
			if (newMaps != null)
				newSearch.SetSearchMaps(newMaps, false);


			// If you loaded from a search that chose the map, but didn't choose, I guess we'll choose for you.
			if (newSearch.parameters.mapType == SearchMapType.ChosenMaps && newSearch.parameters.searchMaps.Count == 0)
				newSearch.SetSearchMap(Find.CurrentMap, false);



			newSearch.children = children.Clone(newSearch);


			newSearch.RemakeList();

			return newSearch;
		}

		private Map boundMap;
		public void UnbindMap() => boundMap = null;
		private void BindToMap(Map map)
		{
			if (boundMap == map) return;

			boundMap = map;

			DoResolveRef(boundMap);
		}


		public void DoResolveRef(Map map)
		{
			Children.ForEach(f => f.DoResolveRef(map));
		}



		// Here we are finally
		// Actually searching and finding the list of things:
		public void RemakeList()
		{
			changed = true;

			// inactive = Don't do anything!
			if (!active)
				return;

			// Set up the maps:
			result.resultMaps.Clear();
			if (CurMap())
				result.resultMaps.Add(Find.CurrentMap);
			else if (AllMaps())
				result.resultMaps.AddRange(Find.Maps);
			else
				result.resultMaps.AddRange(parameters.searchMaps);


			// Peform the search on the maps:
			result.allThings.Clear();
			result.mapThings.Clear();

			foreach (Map map in result.resultMaps)
			{
				List<Thing> things = new(Get(map, parameters.listType));

				// SORT. TODO: more sensical than shortHash.
				things.SortBy(t => t.def.shortHash, t => t.Stuff?.shortHash ?? 0, t => t.Position.x + t.Position.z * 1000);

				result.mapThings[map] = things;
				result.allThings.AddRange(things);
			}

			newListedThings.Clear();

			//Btw, were we looking with godmode?
			result.godMode = DebugSettings.godMode;
		}

		private List<Thing> newListedThings = new();
		private List<Thing> newFilteredThings = new();
		private List<Thing> Get(Map searchMap, SearchListType searchListType)
		{ 
			BindToMap(searchMap);


			// newListedThings is what we're gonna return
			newListedThings.Clear();


			// Check visibility, and for particularly extensive searches, validity.
			Func<Thing, bool> visible = DebugSettings.godMode ? null : t => !t.Position.Fogged(searchMap);
			Func<Thing, bool> valid = DebugSettings.godMode ? null : t => ValidDef(t.def) && !t.Position.Fogged(searchMap);


			// First if we're listing from AllThings, only do that
			if (searchListType.HasFlag(SearchListType.All))
			{
				newListedThings.AddRange(searchMap.listerThings.AllThings.MaybeWhere(valid));
			}
			else if (searchListType.HasFlag(SearchListType.Selectable))
			{
				newListedThings.AddRange(searchMap.listerThings.AllThings.Where(t => t.def.selectable).MaybeWhere(valid));
			}
			else
			{
				// Add in individual categories
				if (searchListType.HasFlag(SearchListType.Everyone))
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.Pawn).MaybeWhere(visible));
				if (searchListType.HasFlag(SearchListType.Buildings))
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Where(t => t.def.filthLeaving != ThingDefOf.Filth_RubbleRock).MaybeWhere(visible));
				if (searchListType.HasFlag(SearchListType.Plants))
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Where(t => t.def.selectable).MaybeWhere(visible));


				// Add in Items/Junk, which overlap as "HaulableEver"
				if (searchListType.HasFlag(SearchListType.Items | SearchListType.Junk)) //To be read "Items AND Junk" sorry
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).MaybeWhere(visible));
				else if (searchListType.HasFlag(SearchListType.Items))
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways).MaybeWhere(visible));
				else if (searchListType.HasFlag(SearchListType.Junk))
					newListedThings.AddRange(searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t => !t.def.alwaysHaulable).MaybeWhere(visible));
				

				//A few other weird types
				if (searchListType.HasFlag(SearchListType.Natural))
					newListedThings.AddRange(searchMap.listerThings.AllThings.Where(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock).MaybeWhere(visible));

				if (searchListType.HasFlag(SearchListType.Inventory))
				{
					foreach (Thing t in searchMap.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder).MaybeWhere(visible))
						if (t is IThingHolder holder && t is not Corpse && t is not MinifiedThing)
							ContentsUtility.AddAllKnownThingsInside(holder, newListedThings);
				}

				//Silly devmode options
				if (searchListType.HasFlag(SearchListType.Haulables))
					newListedThings.AddRange(searchMap.listerHaulables.ThingsPotentiallyNeedingHauling().MaybeWhere(visible));
				if (searchListType.HasFlag(SearchListType.Mergables))
					newListedThings.AddRange(searchMap.listerMergeables.ThingsPotentiallyNeedingMerging().MaybeWhere(visible));
				if (searchListType.HasFlag(SearchListType.FilthInHomeArea))
					newListedThings.AddRange(searchMap.listerFilthInHomeArea.FilthInHomeArea.MaybeWhere(visible));
			}

			// Apply the actual queries, finally

			var queries = Children.queries.FindAll(f => f.Enabled);
			if (MatchAllQueries)
			{
				// ALL
				foreach (ThingQuery query in queries)
				{
					// Clears newQueriedThings, fills with newListedThings which pass the query.
					query.Apply(newListedThings, newFilteredThings);

					// newQueriedThings is now the list of things ; swap them
					(newListedThings, newFilteredThings) = (newFilteredThings, newListedThings);
				}
			}
			else
			{
				// ANY

				newFilteredThings.Clear();
				foreach (Thing thing in newListedThings)
					if (queries.Any(f => f.AppliesTo(thing)))
						newFilteredThings.Add(thing);

				(newListedThings, newFilteredThings) = (newFilteredThings, newListedThings);
			}

			newFilteredThings.Clear();

			return newListedThings;
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			!typeof(Projectile).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;	//non-drawers are weird abstract things.
	}
}