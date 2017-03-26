﻿using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.Cred.Data
{
	internal sealed class PasswordGenerator : ObservableObject
	{
		private readonly SimpleCommand generatePasswordCommand;

		private bool[] flags = { true, true, true, true };
		private int length = 8;

		private string decrypted;
		private string encrypted;

		public PasswordGenerator()
		{
			generatePasswordCommand = new SimpleCommand(
				p => GenerateNextPassword(),
				p => length >= 4 && flags.Any(c => c)
			);
		} // ctor

		private int GetPoolLength()
		{
			var poolLength = 0;
			for (var i = 0; i < flags.Length; i++)
			{
				if (flags[i])
					poolLength += staticPool[i].Item2;
			}
			return poolLength;
		} // func GetPoolLength

		private int GetPoolIndex(int v)
		{
			for (var i = 0; i < flags.Length; i++)
			{
				if (flags[i])
				{
					v = v - staticPool[i].Item2;
					if (v < 0)
						return i;
				}
			}
			throw new InvalidOperationException();
		} // func GetPoolIndex

		public void GenerateNextPassword()
		{
			var poolLength = GetPoolLength();
			var poolUsed = new bool[] { false, false, false, false };
			var rnd = new Random(Environment.TickCount);
			var chars = new char[length];

			char GetRndChar(int pi)
			{
				var charPool = staticPool[pi].Item1;
				return charPool[rnd.Next(0, charPool.Length - 1)];
			}

			// fill chars
			for (var i = 0; i < length; i++)
			{
				var poolIndex = GetPoolIndex(rnd.Next(0, poolLength));
				poolUsed[poolIndex] = true;
				chars[i] = GetRndChar(poolIndex);
			}

			// fill unused chars (at least one)
			for (var i = 0; i < flags.Length; i++)
			{
				if (flags[i] && !poolUsed[i])
					chars[rnd.Next(0, length - 1)] = GetRndChar(i);
			}

			// set the password
			Decrypted = new string(chars);
		} // proc GenerateNextPassword

		private void EncryptPassword(string value)
		{
			// get current mode
			var m = String.IsNullOrEmpty(encrypted) ? '2' : encrypted[0];
			string newEncrypted;
			switch (m)
			{
				case '0': // none
				default:
					newEncrypted = "0" + value;
					break;
				//case '1':
				//	if (String.IsNullOrEmpty(value))
				//		newEncrypted = "1";
				//	else
				//	{
				//		using (var ss = value.CreateSecureString())
				//			newEncrypted = ss.EncryptString(EncryptMethod.SimpleXor);
				//	}
				//	break;
				//default:
				//	if (String.IsNullOrEmpty(value))
				//		newEncrypted = "2";
				//	else
				//	{
				//		using (var ss = value.CreateSecureString())
				//			newEncrypted = ss.EncryptString(EncryptMethod.DES); // DES with default key
				//	}
				//	break;
			}

			if (newEncrypted != encrypted)
			{
				encrypted = newEncrypted;
				OnPropertyChanged(nameof(Encrypted));
			}
		} // proc EncryptPassword

		private void DecryptPassword(string value)
		{
			if (value == null || value.Length == 1)
				decrypted = String.Empty;
			else
			{
				//using (var ss = value.DecryptString())
				//	decrypted = ss.GetPassword();
			}
			OnPropertyChanged(nameof(Decrypted));
		} // proc DecryptPassword

		public string Decrypted
		{
			get => decrypted;
			set
			{
				if (decrypted != value)
				{
					decrypted = value;
					EncryptPassword(value);
					OnPropertyChanged(nameof(Decrypted));
				}
			}
		} // prop Decrypted

		public string Encrypted
		{
			get => encrypted;
			set
			{
				if (encrypted != value)
				{
					encrypted = value;
					DecryptPassword(value);
					OnPropertyChanged(nameof(Encrypted));
				}
			}
		} // prop Encrypted

		private void SetProperty(string propertyName, int flagIndex, bool value)
		{
			SetProperty(propertyName, ref flags[flagIndex], value);
			generatePasswordCommand.Refresh();
		} // proc SetProperty

		/// <summary>Generate password with letters</summary>
		public bool GenerateLetters { get => flags[generateLetters]; set => SetProperty(nameof(GenerateLetters), generateLetters, value); }
		/// <summary>Generate password with digits</summary>
		public bool GenerateDigits { get => flags[generateDigits]; set => SetProperty(nameof(GenerateDigits), generateDigits, value); }
		/// <summary>Generate password with case sensitive</summary>
		public bool GenerateCaseSensitive { get => flags[generateCaseSensitive]; set => SetProperty(nameof(GenerateCaseSensitive), generateCaseSensitive, value); }
		/// <summary>Generate password with special chars</summary>
		public bool GenerateSpecial { get => flags[generateSpecial]; set => SetProperty(nameof(GenerateSpecial), generateSpecial, value); }

		public int GenerateLength { get => length; set { SetProperty(nameof(GenerateLength), ref length, value); generatePasswordCommand.Refresh(); } }

		public ICommand GeneratePasswordCommand => generatePasswordCommand;

		// -- Static ----------------------------------------------------------

		private const int generateLetters = 0;
		private const int generateCaseSensitive = 1;
		private const int generateDigits = 2;
		private const int generateSpecial = 3;

		private static char[] poolLetters = new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'm', 'n', 'o', 'p', 'q', 'e', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
		private static char[] poolLettersCap = new[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'E', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
		private static char[] poolDigits = new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' };
		private static char[] poolSpecial = new[] { ',', '.', '-', '#', '+', ';', ':', '_', '\'', '*', '~', '<', '>', '|', '!', '"', '§', '$', '%', '&', '/', '(', ')', '=', '?', '{', '[', ']', '}', '\\' };

		private static Tuple<char[], int>[] staticPool = new[]
		{
			new Tuple<char[], int>(poolLetters, 18),
			new Tuple<char[], int>(poolLettersCap, 6),
			new Tuple<char[], int>(poolDigits, 3),
			new Tuple<char[], int>(poolSpecial, 1)
		};
	} // class PasswordGenerator
}
