﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;

namespace TDFindLib_Royalty
{
	public class ThingQueryRoyalty : ThingQuery
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			true;
	}
}
