﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AdjustableLeveling.Leveling
{
	public class AdjustableCharacterDevelopmentModel : DefaultCharacterDevelopmentModel
	{
		private static readonly TextObject _skillFocusText;
		private static readonly TextObject _overLimitText;

		private readonly int[] _skillsRequiredForLevel = new int[1025];

		public override int MaxAttribute => 
			AdjustableLeveling.Settings.MaxAttribute;
		public override int MaxFocusPerSkill => 
			AdjustableLeveling.Settings.MaxFocusPerSkill;
		public override int FocusPointsPerLevel => 
			AdjustableLeveling.Settings.FocusPointsPerLevel;
		public override int FocusPointsAtStart => 
			AdjustableLeveling.Settings.FocusPointsAtStart;
		public override int AttributePointsAtStart => 
			AdjustableLeveling.Settings.AttributePointsAtStart;
		public override int LevelsPerAttributePoint => 
			AdjustableLeveling.Settings.LevelsPerAttributePoint;

		static AdjustableCharacterDevelopmentModel() 
		{
			try
			{
				_skillFocusText = (TextObject)AccessTools.Field(typeof(DefaultCharacterDevelopmentModel), nameof(_skillFocusText)).GetValue(null);
				_overLimitText = (TextObject)AccessTools.Field(typeof(DefaultCharacterDevelopmentModel), nameof(_overLimitText)).GetValue(null);
			}
			catch (Exception exc)
			{
				AdjustableLeveling.Message($"ERROR: Adjustable Leveling failed to initialize (static {nameof(AdjustableCharacterDevelopmentModel)}): {exc.GetType()}: {exc.Message}\n{exc.StackTrace}");
			}
		}

		public AdjustableCharacterDevelopmentModel() : base()
		{
			try
			{
				// overwrite private _skillsRequiredForLevel-field
				GenerateSkillsRequiredForLevel();
			}
			catch (Exception exc)
			{
				AdjustableLeveling.Message($"ERROR: Adjustable Leveling failed to initialize (public {nameof(AdjustableCharacterDevelopmentModel)}): {exc.GetType()}: {exc.Message}\n{exc.StackTrace}");
			}
		}

		private void GenerateSkillsRequiredForLevel()
		{
			_skillsRequiredForLevel[0] = 0;
			for (int i = 1; i < _skillsRequiredForLevel.Length; i++)
				_skillsRequiredForLevel[i] = _skillsRequiredForLevel[i - 1] + (int)(AdjustableLeveling.Settings.UseFasterLevelingCurve ? 500f * MathF.Pow(i, 2f) : 25f * MathF.Pow(i, 3f));
			AccessTools.Field(typeof(DefaultCharacterDevelopmentModel), nameof(_skillsRequiredForLevel)).SetValue(this, _skillsRequiredForLevel);
		}


		public override int SkillsRequiredForLevel(int level) =>
			(level > AdjustableLeveling.Settings.MaxCharacterLevel || level >= _skillsRequiredForLevel.Length) ? int.MaxValue : _skillsRequiredForLevel[level];

		public override ExplainedNumber CalculateLearningLimit(int attributeValue, int focusValue, TextObject attributeName, bool includeDescriptions = false)
		{
			var explainedNumber = new ExplainedNumber(AdjustableLeveling.Settings.BaseLearningLimit, includeDescriptions);
			explainedNumber.Add(
				focusValue * AdjustableLeveling.Settings.LearningLimitIncreasePerFocusPoint, 
				_skillFocusText);
			explainedNumber.Add(
				attributeValue * AdjustableLeveling.Settings.LearningLimitIncreasePerAttributePoint,
				attributeName);
			explainedNumber.LimitMin(0f);
			return explainedNumber;
		}

		public override ExplainedNumber CalculateLearningRate(int attributeValue, int focusValue, int skillValue, int characterLevel, TextObject attributeName, bool includeDescriptions = false)
		{
			int baseLimit = AdjustableLeveling.Settings.BaseLearningLimit;
			int focusLimit = focusValue * AdjustableLeveling.Settings.LearningLimitIncreasePerFocusPoint;
			int attributeLimit = attributeValue * AdjustableLeveling.Settings.LearningLimitIncreasePerAttributePoint;

			int focusMax = baseLimit + focusLimit;
			int finalMax = focusMax + attributeLimit;

			var explainedNumber = new ExplainedNumber(0f, includeDescriptions);
			if (skillValue < focusMax)
			{
				var factor = 1f - skillValue / (float)focusMax;
				explainedNumber.Add((0.25f + 0.5f * factor) * attributeValue, attributeName);
				explainedNumber.Add((0.50f + factor) * focusValue, _skillFocusText);
			}
			else if (skillValue < finalMax)
			{
				var factor = 1f - (skillValue - focusMax) / (float)attributeLimit;
				explainedNumber.Add(0.25f * factor * attributeValue, attributeName);
				explainedNumber.Add(0.50f * factor * focusValue, _skillFocusText);
			}
			else
			{
				explainedNumber.Add(-1f, _overLimitText);
			}

			explainedNumber.LimitMin(AdjustableLeveling.Settings.MinLearningRate);
			if (AdjustableLeveling.Settings.MaxLearningRate > 0f)
				explainedNumber.LimitMax(AdjustableLeveling.Settings.MaxLearningRate);
			return explainedNumber;
		}
	}
}
