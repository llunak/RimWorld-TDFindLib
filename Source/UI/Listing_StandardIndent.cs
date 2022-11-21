﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	public class Listing_StandardIndent : Listing_Standard
	{
		float totalIndent;
		private Stack<float> indentSizes = new Stack<float>();
		private Stack<float> indentHeights = new Stack<float>();

		public void NestedIndent(float size)
		{
			Indent(size);
			totalIndent += size;
			SetWidthForIndent();
			indentSizes.Push(size);
			indentHeights.Push(curY);
		}

		public void NestedOutdent()
		{
			if (indentSizes.Count > 0)
			{
				float size = indentSizes.Pop();
				Outdent(size);
				totalIndent -= size;
				SetWidthForIndent();

				//Draw vertical line marking indention
				float startHeight = indentHeights.Pop();
				GUI.color = Color.grey;
				Widgets.DrawLineVertical(curX, startHeight, curY - startHeight - verticalSpacing);//TODO columns?
				GUI.color = Color.white;
			}
		}

		public void SetWidthForIndent()
		{
			ColumnWidth = listingRect.width - totalIndent;
		}
		
		public void BeginScrollView(Rect rect, ref Vector2 scrollPosition, Rect viewRect, GameFont font = GameFont.Small)
		{
			//Widgets.BeginScrollView(rect, ref scrollPosition, viewRect, true);
			//rect.height = 100000f;
			//rect.width -= 20f;
			//this.Begin(rect.AtZero());

			//Need BeginGroup before ScrollView, listingRect needs rect.width-=20 but the group doesn't

			Widgets.BeginGroup(rect);
			Widgets.BeginScrollView(rect.AtZero(), ref scrollPosition, viewRect, true);
			
			maxOneColumn = true;

			rect.width -= 20f;
			listingRect = rect;
			ColumnWidth = listingRect.width;

			curX = 0f;
			curY = 0f;

			Text.Font = font;
		}

		//1.3 just removed Listing Scrollviews?
		public void EndScrollView(ref float listingHeight)
		{
			listingHeight = curY;
			Widgets.EndScrollView();
			End();
		}

		public void Header(TaggedString label, float maxHeight = -1, string tooltip = null)
		{
			var lastFont = Text.Font;
			Text.Font = GameFont.Medium;
			Label(label, maxHeight, tooltip);
			Text.Font = lastFont;
		}

		public bool CheckboxLabeledChanged(string label, ref bool checkOn, string tooltip = null, float height = 0f, float labelPct = 1f)
		{
			bool prev = checkOn;

			CheckboxLabeled(label, ref checkOn, tooltip, height, labelPct);

			return prev != checkOn;
		}

		public Rect GetRemainingRect(float widthPct = 1f)
		{
			float remaining = listingRect.height - curY;
			Rect result = new Rect(curX, curY, ColumnWidth * widthPct, remaining);
			curY = listingRect.height;
			return result;
		}
	}
}
