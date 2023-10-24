#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Neo.PerfectWorking.UI
{
	#region -- class PwColor ----------------------------------------------------------

	/// <summary>Pps color definition</summary>
	public sealed class PwColor : IEquatable<PwColor>
	{
		private readonly string name;
		private readonly Func<PwTheme, Color> callbackValue;

		private readonly object colorKey;
		private readonly object brushKey;

		/// <summary>Define a color.</summary>
		/// <param name="name"></param>
		/// <param name="callbackValue"></param>
		public PwColor(string name, Func<PwTheme, Color> callbackValue)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.callbackValue = callbackValue ?? throw new ArgumentNullException(nameof(callbackValue));

			RegisterColor(this);

			colorKey = PwTheme.CreateColorKey(this);
			brushKey = PwTheme.CreateBrushKey(this);
		} // ctor

		/// <inheritdoc/>
		public override string ToString()
			=> name;

		/// <inheritdoc/>
		public override int GetHashCode()
			=> name.GetHashCode();

		/// <inheritdoc/>
		public override bool Equals(object obj)
			=> obj is PwColor color && Equals(color);

		/// <summary>Compare the color key.</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PwColor other)
			=> other.name == name;

		/// <summary>Get the themed color.</summary>
		/// <param name="theme"></param>
		/// <returns></returns>
		public Color GetColor(PwTheme theme)
			=> theme.GetThemedColor(this);

		/// <summary>Get the color.</summary>
		/// <param name="theme"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public Color GetCallbackColor(PwTheme theme)
			=> callbackValue(theme);

		/// <summary>Name of the color</summary>
		public string Name => name;

		/// <summary>Resource key for the <see cref="Color" /></summary>
		public object ColorKey => colorKey;
		/// <summary>Resource key for the <see cref="SolidColorBrush"/></summary>
		public object BrushKey => brushKey;

		/// <summary>Empty color</summary>
		public static Color Empty => Color.FromArgb(0, 0, 0, 0);

		// -- Static ----------------------------------------------------------

		private static readonly Dictionary<string, PwColor> colors = new Dictionary<string, PwColor>();

		private static void RegisterColor(PwColor color)
			=> colors.Add(color.Name, color);

		/// <summary>Get color by name</summary>
		/// <param name="name"></param>
		/// <param name="color"></param>
		/// <returns></returns>
		public static bool TryGet(string name, out PwColor color)
			=> colors.TryGetValue(name, out color);

		/// <summary>Get color by name</summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static PwColor Get(string name)
			=> TryGet(name, out var color) ? color : throw new ArgumentOutOfRangeException(nameof(name), name, $"Unknown color name: {name}");

		/// <summary>Defined colors</summary>
		public static IEnumerable<PwColor> Defined
			=> colors.Values;

		#region -- Colors -------------------------------------------------------------

		/// <summary>Contrast color</summary>
		public static readonly PwColor Black = new PwColor(nameof(Black), theme => Color.FromArgb(255, 0, 0, 0));
		/// <summary>Contrast color</summary>
		public static readonly PwColor White = new PwColor(nameof(White), theme => Color.FromArgb(255, 255, 255, 255));
		/// <summary>Application foreground color.</summary>
		public static readonly PwColor Accent = new PwColor(nameof(Accent), theme => Color.FromArgb(255, 80, 192, 0));

		/// <summary>Basis-Hintergrund</summary>
		public static readonly PwColor ControlBackground = new PwColor(nameof(ControlBackground), theme => theme.GetThemedColor(White));
		/// <summary>Basis-Fordergrund</summary>
		public static readonly PwColor ControlForeground = new PwColor(nameof(ControlBackground), theme => theme.GetThemedColor(Black));

		#endregion
	} // class PwColor

	#endregion

	#region -- class PwColorTheme -----------------------------------------------------

	/// <summary>Color and icon theme for the application.</summary>
	public class PwTheme : IEnumerable<PwColor>
	{
		#region -- enum PwColorType ---------------------------------------------------

		private enum PwColorType
		{
			Color,
			Brush
		} // enum PpsColorType

		#endregion

		#region -- class PwColorTypeKey -----------------------------------------------

		private sealed class PpsColorTypeKey
		{
			private readonly PwColorType type;
			private readonly PwColor color;

			public PpsColorTypeKey(PwColorType type, PwColor color)
			{
				this.type = type;
				this.color = color ?? throw new ArgumentNullException(nameof(color));
			} // ctor

			public override string ToString()
				=> color.Name + type.ToString();

			public override int GetHashCode()
				=> type.GetHashCode() ^ color.GetHashCode();

			public override bool Equals(object obj)
				=> obj is PpsColorTypeKey k && k.type == type && k.color.Equals(color);

			public PwColorType Type => type;
			public PwColor Color => color;
		} // class PpsColorType

		#endregion

		#region -- class PwThemeDictionary --------------------------------------------

		private sealed class PwThemeDictionary : ResourceDictionary
		{
			private readonly PwTheme theme;

			public PwThemeDictionary(PwTheme theme)
			{
				this.theme = theme ?? throw new ArgumentNullException(nameof(theme));

				foreach (var c in theme)
				{
					Add(c.BrushKey, c);
					Add(c.BrushKey.ToString(), c.BrushKey);
					Add(c.ColorKey, c);
					Add(c.ColorKey.ToString(), c.ColorKey);
				}
			} // ctor

			protected override void OnGettingValue(object key, ref object value, out bool canCache)
			{
				if (key is string && value is PpsColorTypeKey valueColorType)
				{
					value = this[valueColorType];
					canCache = true;
				}
				else if (key is PpsColorTypeKey colorType && value is PwColor colorKey)
				{
					switch (colorType.Type)
					{
						case PwColorType.Brush:
							value = new SolidColorBrush(colorKey.GetColor(theme));
							break;
						case PwColorType.Color:
							value = colorKey.GetColor(theme);
							break;
						default:
							value = null;
							break;
					}

					// freeze value
					if (value is Freezable f && f.CanFreeze)
						f.Freeze();
					canCache = true;
				}
				else
					base.OnGettingValue(key, ref value, out canCache);
			} // func OnGettingValue
		} // class PpsColorThemeDictionary

		#endregion

		private readonly string name;
		private readonly Dictionary<string, Color> themedColor = new Dictionary<string, Color>();

		/// <summary></summary>
		public PwTheme(string name, IEnumerable<KeyValuePair<string, Color>> colors)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));

			if (colors != null)
			{
				foreach (var c in colors)
					themedColor.Add(c.Key, c.Value);
			}
		} // ctor

		/// <summary>Returns the themed color.</summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public Color GetThemedColor(PwColor color)
			=> themedColor.TryGetValue(color.Name, out var cl) ? cl : color.GetCallbackColor(this);

		/// <summary>Mixes two colors to one color.</summary>
		/// <param name="backColor"></param>
		/// <param name="transparentColor"></param>
		/// <param name="opacity"></param>
		/// <returns></returns>
		public Color GetTransparencyColor(PwColor backColor, PwColor transparentColor, float opacity)
			=> UIHelper.GetMixedColor(backColor.GetColor(this), transparentColor.GetColor(this), opacity);

		/// <summary>Mixes two colors to one color.</summary>
		/// <param name="source">Source color.</param>
		/// <param name="destination">Destination color.</param>
		/// <param name="sourcePart">Distance to pick.</param>
		/// <param name="alpha">Alpha value of the result.</param>
		/// <returns></returns>
		public Color GetAlphaBlendColor(PwColor source, PwColor destination, float sourcePart = 0.5f, float alpha = 1.0f)
			=> UIHelper.GetAlphaColor(source.GetColor(this), destination.GetColor(this), sourcePart, alpha);

		/// <summary>Enumerates all color keys.</summary>
		/// <returns></returns>
		public IEnumerator<PwColor> GetEnumerator()
			=> PwColor.Defined.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Name of the theme</summary>
		public string Name => name;

		internal static object CreateColorKey(PwColor color)
			=> new PpsColorTypeKey(PwColorType.Color, color);

		internal static object CreateBrushKey(PwColor color)
			=> new PpsColorTypeKey(PwColorType.Brush, color);

		/// <summary>Update theme.</summary>
		/// <param name="theme"></param>
		public static void UpdateThemedDictionary(Collection<ResourceDictionary> resources, PwTheme theme)
		{
			for (var i = 0; i < resources.Count; i++)
			{
				if (resources[i] is PwThemeDictionary)
				{
					resources[i] = new PwThemeDictionary(theme);
					return;
				}
			}
			resources.Add(new PwThemeDictionary(theme));
		} // proc UpdateThemedDictionary

		/// <summary>Default color scheme.</summary>
		public static PwTheme Default { get; } = new PwTheme(nameof(Default), null);
	} // class PwTheme

	#endregion
}
