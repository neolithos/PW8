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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Neo.IronLua;
using System.Diagnostics;
using System.IO;

namespace Neo.PerfectWorking.Calc
{
	#region -- class FormularFunctions --------------------------------------------------

	public class FormularFunctions : LuaTable
	{
		#region -- Ctor/Dtor ------------------------------------------------------------

		public FormularFunctions()
		{
			SetMemberValue("pi", Math.PI);
			SetMemberValue("e", Math.E);

			SetMemberValue("kb", 1 << 10);
			SetMemberValue("mb", 1 << 20);
			SetMemberValue("gb", 1 << 30);

			SetMemberValue("abs", CreateMethodSimple(typeof(Math), nameof(Math.Abs), 1));
			SetMemberValue("acos", CreateMethodExact(typeof(Math), nameof(Math.Acos), typeof(double)));
			SetMemberValue("asin", CreateMethodExact(typeof(Math), nameof(Math.Asin), typeof(double)));
			SetMemberValue("atan", CreateMethodExact(typeof(Math), nameof(Math.Atan), typeof(double)));
			SetMemberValue("atan2", CreateMethodExact(typeof(Math), nameof(Math.Atan2), typeof(double), typeof(double)));
			SetMemberValue("ceil", CreateMethodSimple(typeof(Math), nameof(Math.Ceiling), 1));
			SetMemberValue("cos", CreateMethodExact(typeof(Math), nameof(Math.Cos), typeof(double)));
			SetMemberValue("cosh", CreateMethodExact(typeof(Math), nameof(Math.Cosh), typeof(double)));
			SetMemberValue("exp", CreateMethodExact(typeof(Math), nameof(Math.Exp), typeof(double)));
			SetMemberValue("floor", CreateMethodSimple(typeof(Math), nameof(Math.Floor),1));
			SetMemberValue("IEEERemainder", CreateMethodExact(typeof(Math), nameof(Math.IEEERemainder), typeof(double), typeof(double)));
			SetMemberValue("ln", CreateMethodExact(typeof(Math), nameof(Math.Log), typeof(double)));
			SetMemberValue("log", CreateMethodExact(typeof(Math), nameof(Math.Log), typeof(double), typeof(double)));
			SetMemberValue("log10", CreateMethodExact(typeof(Math), nameof(Math.Log10), typeof(double)));
			SetMemberValue("rnd", CreateMethodSimple(typeof(Math), nameof(Math.Round), 1));
			SetMemberValue("round", CreateMethodSimple(typeof(Math), nameof(Math.Round), 2));
			SetMemberValue("sign", CreateMethodSimple(typeof(Math), nameof(Math.Sign), 1));
			SetMemberValue("sin", CreateMethodExact(typeof(Math), nameof(Math.Sin), typeof(double)));
			SetMemberValue("sinh", CreateMethodExact(typeof(Math), nameof(Math.Sinh), typeof(double)));
			SetMemberValue("sqrt", CreateMethodExact(typeof(Math), nameof(Math.Sqrt), typeof(double)));
			SetMemberValue("tan", CreateMethodExact(typeof(Math), nameof(Math.Tan), typeof(double)));
			SetMemberValue("tanh", CreateMethodExact(typeof(Math), nameof(Math.Tanh), typeof(double)));
			SetMemberValue("trunc", CreateMethodSimple(typeof(Math), nameof(Math.Truncate), 1));
		} // ctor

		#endregion

		#region -- CreateMethodSimple/Exact ---------------------------------------------

		private static ILuaMethod CreateMethodWithArguments(Type type, string methodName, Func<MethodInfo, bool> argCheck)
		{
			var methods = (
				from m in type.GetRuntimeMethods()
				where m.IsStatic && m.Name == methodName && argCheck(m)
				select m).ToArray();

			return methods.Length == 1
					? (ILuaMethod)new LuaMethod(null, methods[0])
					: (ILuaMethod)new LuaOverloadedMethod(null, methods);
		} // func CreateMethodWithArguments

		private static ILuaMethod CreateMethodSimple(Type type, string methodName, int argumentCount)
		{
			return CreateMethodWithArguments(type, methodName,
				  m =>
				  {
					  var pi = m.GetParameters();
					  if (pi.Length != argumentCount)
						  return false;

					  for (var i = 0; i < argumentCount; i++)
					  {
						  if (!(pi[i].ParameterType == typeof(long)
							  || pi[i].ParameterType == typeof(int)
							  || pi[i].ParameterType == typeof(decimal)
							  || pi[i].ParameterType == typeof(double)))
							  return false;
					  }
					  return true;
				  }
			  );
		} // func CreateMethodSimple

		private static ILuaMethod CreateMethodExact(Type type, string methodName, params Type[] arguments)
		{
			return CreateMethodWithArguments(type, methodName,
				  m =>
				  {
					  var pi = m.GetParameters();
					  if (pi.Length != arguments.Length)
						  return false;

					  for (var i = 0; i < arguments.Length; i++)
					  {
						  if (pi[i].ParameterType != arguments[i])
							  return false;
					  }
					  return true;
				  }
			  );
		} // func CreateMethodExact

		#endregion

		public void ImportMember(LuaTable table)
		{
			foreach (var kv in table.Members)
				SetMemberValue(kv.Key, kv.Value);
		} // proc ImportMember
	} // class FormularFunctions

	#endregion

	#region -- class FormularEnvironment ------------------------------------------------

	public class FormularEnvironment : LuaTable
	{
		private readonly FormularFunctions functions;

		public FormularEnvironment(FormularFunctions functions)
		{
			this.functions = functions ?? new FormularFunctions();
		} // ctor

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? functions.GetValue(key);
	} // class FormularEnvironment

	#endregion

	#region -- class Formular -----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Parser für Formeln.</summary>
	public sealed class Formular
	{
		#region -- enum TokenType -------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public enum TokenType
		{
			Empty = -2,
			Error = -1,
			Eof,

			// -- Operatoren --
			Plus,         // +
			Minus,        // -
			Star,         // *
			Slash,        // /
			Backshlash,   // \   div
			Percent,      // %   mod
			BitAnd,       // &
			BitOr,        // |
			BitXOr,       // ^
			BitNot,       // ~
			Faculty,      // !
			Power,        // **
			Root,         // //
			ShiftLeft,    // <<
			ShiftRight,   // >>
			BracketOpen,  // (
			BracketClose, // )
			Equal,        // =
			Semi,         // ;
			Raute,        // #
			Colon,        // :

			// -- Operanten --
			Identifier,
			Number
		} // enum TokenType

		#endregion

		#region -- struct Token ---------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public struct Token
		{
			public bool SetToken(TokenType type, int startAt, object value)
			{
				this.Type = type;
				this.Position = startAt;
				this.Length = 0;
				this.Value = value;
				return true;
			} // proc SetToken

			public bool SetToken(TokenType type, int startAt, int endAt, object value)
			{
				this.Type = type;
				this.Position = startAt;
				this.Length = endAt - startAt;
				this.Value = value;
				return true;
			} // proc SetToken

			public override string ToString()
				=> $"Token {Type} [{Position},{Length}]: {(Value ?? "<null>")}";
		
			public TokenType Type { get; set; }
			public int Position { get; set; }
			public int Length { get; set; }
			public object Value { get; set; }
		} // struct Token

		#endregion

		#region -- enum StackValueType --------------------------------------------------

		private enum StackValueType
		{
			Null = 0,
			Integer = 1,
			Decimal = 2,
			Double = 3
		} // enum StackValueType

		#endregion

		#region -- struct StackValue ----------------------------------------------------

		private struct StackValue
		{
			private readonly StackValueType type;
			private readonly object value;

			public StackValue(double value)
			{
				this.type = StackValueType.Double;
				this.value = value;
			} // ctor

			public StackValue(decimal value)
			{
				this.type = StackValueType.Decimal;
				this.value = value;
			} // ctor

			public StackValue(long value)
			{
				this.type = StackValueType.Integer;
				this.value = value;
			} // ctor

			private StackValue(StackValueType type, object value)
			{
				this.type = type;
				this.value = value;
			} // ctor

			public override string ToString()
				=> $"({type}){value}";

			public StackValue ConvertTo(StackValueType t)
			{
				switch (t)
				{
					case StackValueType.Integer:
						switch (type)
						{
							case StackValueType.Null:
								return new StackValue(0L);
							case StackValueType.Integer:
								return this;
							case StackValueType.Decimal:
								return new StackValue(Convert.ToInt64(ValueDecimal));
							case StackValueType.Double:
								return new StackValue(Convert.ToInt64(ValueDouble));
							default:
								throw new ArgumentOutOfRangeException();
						}
					case StackValueType.Decimal:
						switch (type)
						{
							case StackValueType.Null:
								return new StackValue(0m);
							case StackValueType.Integer:
								return new StackValue(Convert.ToDecimal(ValueLong));
							case StackValueType.Decimal:
								return this;
							case StackValueType.Double:
								return new StackValue(Convert.ToDecimal(ValueDouble));
							default:
								throw new ArgumentOutOfRangeException();
						}
					case StackValueType.Double:
						switch (type)
						{
							case StackValueType.Null:
								return new StackValue(0.0);
							case StackValueType.Integer:
								return new StackValue(Convert.ToDouble(ValueLong));
							case StackValueType.Decimal:
								return new StackValue(Convert.ToDouble(ValueDecimal));
							case StackValueType.Double:
								return this;
							default:
								throw new ArgumentOutOfRangeException();
						}
					default:
						throw new ArgumentOutOfRangeException();
				}
			}  // func ConvertTo

			public StackValueType Type => type;

			public object Value => value;
			public long ValueLong => (long)value;
			public decimal ValueDecimal => (decimal)value;
			public double ValueDouble => (double)value;

			public bool IsEmpty => value != null;

			// -- Static ------------------------------------------------------

			public static StackValue FromObject(object value, bool useDecimal)
			{
				if (value == null)
					return new StackValue(StackValueType.Null, 0);
				else
				{
					switch (System.Type.GetTypeCode(value.GetType()))
					{
						case TypeCode.Int64:
							return new StackValue(StackValueType.Integer, value);
						case TypeCode.Decimal:
							return useDecimal
								? new StackValue(StackValueType.Decimal, value)
								: new StackValue(Convert.ToDouble((decimal)value));
						case TypeCode.Double:
							return useDecimal
								? new StackValue(Convert.ToDecimal((double)value))
								: new StackValue(StackValueType.Double, value);
						case TypeCode.Single:
							return useDecimal
								? new StackValue(Convert.ToDecimal((float)value))
								: new StackValue(Convert.ToDouble((float)value));

						case TypeCode.Boolean:
							return new StackValue((bool)value ? -1L : 0L);
						case TypeCode.Byte:
							return new StackValue((long)(byte)value);
						case TypeCode.UInt16:
							return new StackValue((long)(ushort)value);
						case TypeCode.UInt32:
							return new StackValue((long)(uint)value);
						case TypeCode.UInt64:
							try
							{
								return new StackValue(checked((long)(ulong)value));
							}
							catch (OverflowException)
							{
								return useDecimal
									? new StackValue(Convert.ToDecimal((ulong)value))
									: new StackValue(Convert.ToDouble((ulong)value));
							}
						case TypeCode.SByte:
							return new StackValue((long)(sbyte)value);
						case TypeCode.Int16:
							return new StackValue((long)(short)value);
						case TypeCode.Int32:
							return new StackValue((long)(int)value);

						default:
							throw new InvalidDataException(); // todo: runtime exception
					}
				}
			} // func FromObject
		} // struct StackValue

		#endregion

		#region -- class StackMachine ---------------------------------------------------

		private sealed class StackMachine
		{
			private readonly Formular formular;
			private readonly StackValue[] stack;

			private int stackPtr = -1;

			public StackMachine(Formular formular, int stackSize)
			{
				this.formular = formular;
				this.stack = new StackValue[stackSize];
			} // ctor

			public void Push(StackValue v)
				=> stack[++stackPtr] = v;

			public StackValue Peek()
				=> stack[stackPtr];

			public StackValue Pop()
				=> stack[stackPtr--];

			public bool IsEmpty => stackPtr == -1;
			public Formular Formular => formular;
			public bool UseDecimal => formular.useDecimal;
		} // class StackMachine

		#endregion

		#region -- class InstructionBase ------------------------------------------------

		private abstract class InstructionBase
		{
			public abstract void Execute(StackMachine stack);
		} // class InstructionBase

		#endregion

		#region -- class PushInstruction ------------------------------------------------

		private sealed class PushInstruction : InstructionBase
		{
			private readonly StackValue value;

			public PushInstruction(StackValue value)
			{
				this.value = value;
			} // ctor

			public override string ToString()
				=> "Push " + value.ToString();

			public override void Execute(StackMachine stack)
				=> stack.Push(value);
		} // class PushInstruction

		#endregion

		#region -- enum UnaryInstructionType --------------------------------------------

		private enum UnaryInstructionType
		{
			Negate,
			OnesComplement,
			Faculity
		} // enum SolveUnaryExpressionType

		#endregion

		#region -- class UnaryInstruction -----------------------------------------------

		private sealed class UnaryInstruction : InstructionBase
		{
			private readonly UnaryInstructionType type;

			public UnaryInstruction(UnaryInstructionType type)
			{
				this.type = type;
			} // ctor

			public override string ToString() 
				=> type.ToString();
			
			private static void UnaryOperation(StackMachine stack, Func<long, long> longOperation, Func<decimal, decimal> decimalOperation, Func<double, double> doubleOperation)
			{
				var v = stack.Pop();
				switch (v.Type)
				{
					case StackValueType.Null:
						stack.Push(new StackValue(longOperation(0L)));
						break;
					case StackValueType.Decimal:
						stack.Push(new StackValue(decimalOperation(v.ValueDecimal)));
						break;
					case StackValueType.Double:
						stack.Push(new StackValue(doubleOperation(v.ValueDouble)));
						break;
					case StackValueType.Integer:
						stack.Push(new StackValue(longOperation(v.ValueLong)));
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(v));
				}
			} // proc UnaryOperation

			private static long FaculityLong(long n)
				=> n == 0 ? 1 : checked(n * FaculityLong(n - 1));

			private static decimal FaculityDecimal(decimal n)
				=> n == 0m ? 1m : checked(n * FaculityDecimal(n - 1m));

			private static double FaculityDouble(double n)
				=> n == 0.0 ? 1.0 : checked(n * FaculityDouble(n - 1.0));

			public override void Execute(StackMachine stack)
			{
				switch (type)
				{
					case UnaryInstructionType.Negate:
						UnaryOperation(stack,
							c => -c,
							c => -c,
							c => -c
						);
						break;
					case UnaryInstructionType.OnesComplement:
						UnaryOperation(stack,
							c => ~c,
							c => -c,
							c => -c
						);
						break;
					case UnaryInstructionType.Faculity:
						UnaryOperation(stack, FaculityLong, FaculityDecimal, FaculityDouble);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(type));
				}
			} // proc Execute
		} // class UnaryInstruction

		#endregion

		#region -- enum BinaryInstructionType -------------------------------------------

		private enum BinaryInstructionType
		{
			Add,
			Subtract,
			Multiply,
			Divide,
			Root,
			Power
		} // enum BinaryInstructionType

		#endregion

		#region -- class BinaryInstruction ----------------------------------------------

		private sealed class BinaryInstruction : InstructionBase
		{
			private readonly BinaryInstructionType type;

			public BinaryInstruction(BinaryInstructionType type)
			{
				this.type = type;
			} // ctor

			public override string ToString()
				=> type.ToString();

			private long BinaryOperation(long v1, long v2)
			{
				switch (type)
				{
					case BinaryInstructionType.Add:
						return checked(v1 + v2);
					case BinaryInstructionType.Subtract:
						return checked(v1 - v2);
					case BinaryInstructionType.Multiply:
						return checked(v1 * v2);
					case BinaryInstructionType.Divide:
						if (v1 % v2 == 0)
							return v1 / v2;
						else
							throw new OverflowException();
					case BinaryInstructionType.Power:
						return checked(Convert.ToInt64(Math.Pow(v1, v2)));
					case BinaryInstructionType.Root:
						throw new OverflowException();
					default:
						throw new ArgumentOutOfRangeException();
				}
			} // func BinaryOperation

			private decimal BinaryOperation(decimal v1, decimal v2)
			{
				switch (type)
				{
					case BinaryInstructionType.Add:
						return v1 + v2;
					case BinaryInstructionType.Subtract:
						return v1 - v2;
					case BinaryInstructionType.Multiply:
						return v1 * v2;
					case BinaryInstructionType.Divide:
						return v1 / v2;
					case BinaryInstructionType.Power:
						return Convert.ToDecimal(Math.Pow(Convert.ToDouble(v1), Convert.ToDouble(v2)));
					case BinaryInstructionType.Root:
						return Convert.ToDecimal(Math.Pow(Convert.ToDouble(v1), 1.0 / Convert.ToDouble(v2)));
					default:
						throw new ArgumentOutOfRangeException();
				}
			} // func BinaryOperation

			private double BinaryOperation(double v1, double v2)
			{
				switch (type)
				{
					case BinaryInstructionType.Add:
						return v1 + v2;
					case BinaryInstructionType.Subtract:
						return v1 - v2;
					case BinaryInstructionType.Multiply:
						return v1 * v2;
					case BinaryInstructionType.Divide:
						return v1 / v2;
					case BinaryInstructionType.Power:
						return Math.Pow(v1, v2);
					case BinaryInstructionType.Root:
						return Math.Pow(v1, 1.0 / v2);
					default:
						throw new ArgumentOutOfRangeException();
				}
			} // func BinaryOperation

			public override void Execute(StackMachine stack)
			{
				var v2 = stack.Pop();
				var v1 = stack.Pop();

				// lift types
				if (v1.Type == StackValueType.Null && v2.Type == StackValueType.Null)
				{
					v1 = v1.ConvertTo(StackValueType.Integer);
					v2 = v2.ConvertTo(StackValueType.Integer);
				}
				else if (v1.Type < v2.Type)
					v1 = v1.ConvertTo(v2.Type);
				else if (v1.Type > v2.Type)
					v2 = v2.ConvertTo(v1.Type);

				switch (v1.Type)
				{
					case StackValueType.Integer:
						try
						{
							stack.Push(new StackValue(BinaryOperation(v1.ValueLong, v2.ValueLong)));
						}
						catch (OverflowException)
						{
							if (stack.Formular.useDecimal)
							{
								v1 = v1.ConvertTo(StackValueType.Decimal);
								v2 = v2.ConvertTo(StackValueType.Decimal);
								goto case StackValueType.Decimal;
							}
							else
							{
								v1 = v1.ConvertTo(StackValueType.Double);
								v2 = v2.ConvertTo(StackValueType.Double);
								goto case StackValueType.Double;
							}
						}
						break;
					case StackValueType.Decimal:
						stack.Push(new StackValue(BinaryOperation(v1.ValueDecimal , v2.ValueDecimal)));
						break;
					case StackValueType.Double:
						stack.Push(new StackValue(BinaryOperation(v1.ValueDouble, v2.ValueDouble)));
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			} // proc Execute
		} // class BinaryInstruction

		#endregion

		#region -- enum IntegerBinaryInstructionType ------------------------------------

		private enum IntegerBinaryInstructionType
		{
			IntModulos,
			IntDivide,
			ShiftLeft,
			ShiftRight,
			BitAnd,
			BitOr,
			BitXOr
		} // enum IntegerBinaryInstructionType

		#endregion

		#region -- class IntegerBinaryInstruction ---------------------------------------
		
		private sealed class IntegerBinaryInstruction : InstructionBase
		{
			private readonly IntegerBinaryInstructionType type;

			public IntegerBinaryInstruction(IntegerBinaryInstructionType type)
			{
				this.type = type;
			} // ctor

			public override string ToString()
				=> type.ToString();

			private long BinaryOperation(long v1, long v2)
			{
				switch (type)
				{
					case IntegerBinaryInstructionType.IntDivide:
						return v1 / v2;
					case IntegerBinaryInstructionType.IntModulos:
						return v1 % v2;
					case IntegerBinaryInstructionType.BitAnd:
						return v1 & v2;
					case IntegerBinaryInstructionType.BitOr:
						return v1 | v2;
					case IntegerBinaryInstructionType.BitXOr:
						return v1 ^ v2;
					default:
						throw new ArgumentOutOfRangeException();
				}
			} // func BinaryOperation

			public override void Execute(StackMachine stack)
			{
				var v2 = stack.Pop();
				var v1 = stack.Pop();

				// convert to integer type
				if (v1.Type != StackValueType.Integer)
					v1 = v1.ConvertTo(StackValueType.Integer);
				if (v2.Type != StackValueType.Integer)
					v2 = v2.ConvertTo(StackValueType.Integer);

				stack.Push(new StackValue(BinaryOperation(v1.ValueLong, v2.ValueLong)));
			} // proc Execute
		} // class IntegerBinaryInstruction

		#endregion

		#region -- class PushVarInstruction ---------------------------------------------

		private sealed class PushVarInstruction : InstructionBase
		{
			private readonly string varName;

			public PushVarInstruction(string varName)
			{
				this.varName = varName;
			} // ctor

			public override string ToString()
				=> "Push " + varName;

			public override void Execute(StackMachine stack)
			{
				stack.Push(StackValue.FromObject(stack.Formular.env.GetMemberValue(varName), stack.UseDecimal));
			} // proc Execute
		} // class PushVarInstruction

		#endregion

		#region -- class StoreVarInstruction --------------------------------------------

		private sealed class StoreVarInstruction : InstructionBase
		{
			private readonly string varName;

			public StoreVarInstruction(string varName)
			{
				this.varName = varName;
			} // ctor

			public override string ToString()
				=> "Store " + varName;

			public override void Execute(StackMachine stack)
			{
				var v1 = stack.Peek();
				stack.Formular.env.SetMemberValue(varName, v1.Value); 
			} // proc Execute
		} // class StoreVarInstruction

		#endregion

		#region -- class CallInstruction ------------------------------------------------

		private sealed class CallInstruction : InstructionBase
		{
			private readonly string funcName;
			private readonly int argumentCount;

			public CallInstruction(string funcName, int argumentCount)
			{
				this.funcName = funcName;
				this.argumentCount = argumentCount;
			} // ctor

			public override string ToString()
				=> $"Call {funcName}@{argumentCount}";

			public override void Execute(StackMachine stack)
			{
				// create argument array
				var args = new object[argumentCount];
				for (var i = argumentCount - 1; i >= 0; i--)
					args[i] = stack.Pop().Value;

				// call the method
				var obj = stack.Formular.env.GetMemberValue(funcName);
				if (Lua.RtInvokeable(obj))
				{
					var r = new LuaResult(Lua.RtInvoke(obj, args));
					obj = r[0];
				}

				stack.Push(StackValue.FromObject(obj, stack.UseDecimal));
			} // proc Execute
		} // class CallInstruction

		#endregion

		#region -- class Parser ---------------------------------------------------------

		private sealed class Parser
		{
			private readonly Formular formular;

			private int currentPosition = 0;
			private Token tok;

			private List<InstructionBase> instructions = new List<InstructionBase>();
			private int estimatedStackSize = 0;
			private int currentStackSize = 0;

			public Parser(Formular formular)
			{
				this.formular = formular;
				formular.ScanToken(ref currentPosition, ref tok);
			} // ctor

			private void Next()
			{
				if (tok.Type == TokenType.Eof)
					return;
				formular.ScanToken(ref currentPosition, ref tok);

				if (tok.Type == TokenType.Error)
					throw new FormularException(tok.Position, tok.Length, (string)tok.Value);
				else if (tok.Type == TokenType.Empty)
					Next();
			} // proc Next

			private void SetStackSize(int newSize)
			{
				if (newSize > estimatedStackSize)
					estimatedStackSize = newSize;
			} // proc SetStackSize

			private void Append(StackValue value)
			{
				instructions.Add(new PushInstruction(value));
				SetStackSize(++currentStackSize);
			} // proc Append

			private void Append(UnaryInstructionType type)
				=> instructions.Add(new UnaryInstruction(type));

			private void Append(BinaryInstructionType type)
			{
				instructions.Add(new BinaryInstruction(type));
				currentStackSize--;
			} // proc Append

			private void Append(IntegerBinaryInstructionType type)
			{
				instructions.Add(new IntegerBinaryInstruction(type));
				currentStackSize--;
			} // proc Append

			private void AppendGet(string identifier)
			{
				instructions.Add(new PushVarInstruction(identifier));
				SetStackSize(currentStackSize++);
			} // proc AppendGet

			private void AppendSet(string identifier)
			{
				instructions.Add(new StoreVarInstruction(identifier));
			} // proc AppendSet

			private void AppendCall(string identifier, int argumentCount)
			{
				instructions.Add(new CallInstruction(identifier, argumentCount));
				currentStackSize -= argumentCount + 1;
			} // proc AppendCall

			private void ParseOperator()
			{
				// collect prefix
				var deleteVar = false;
				var bitNegate = false;
				var negate = false;
				while (tok.Type == TokenType.Plus ||
					   tok.Type == TokenType.Minus ||
					   tok.Type == TokenType.BitNot ||
					   tok.Type == TokenType.Raute)
				{
					if (tok.Type == TokenType.Minus)
						negate = !negate;
					else if (tok.Type == TokenType.BitNot)
						bitNegate = !bitNegate;
					else if (tok.Type == TokenType.Raute)
						deleteVar = !deleteVar;
					Next();
				}

				if (tok.Type == TokenType.BracketOpen) // sub expression
				{
					Next();
					ParseExpr();
					if (tok.Type == TokenType.BracketClose)
						Next();
					else
						throw new FormularException(tok.Position, tok.Length, "Klammer zu erwartet.");
				}
				else if (tok.Type == TokenType.Identifier) // identifier access
				{
					// ident(expr, ...)
					var ident = formular.GetFormularPart(ref tok) ;
					Next();

					if (tok.Type == TokenType.Equal)
					{
						Next();
						ParseExpr();
						AppendSet(ident); // set instruction
					}
					else if (tok.Type == TokenType.BracketOpen) // function
					{
						Next();
						var c = 0;
						while (tok.Type != TokenType.BracketClose)
						{
							ParseExpr();
							if (tok.Type == TokenType.Semi) // optional
								Next();
							c++;
						}
						Next();
						AppendCall(ident, c);
					}
					else
						AppendGet(ident); // get instruction
				}
				else if (tok.Type == TokenType.Number)
				{
					Append(StackValue.FromObject(tok.Value, formular.useDecimal));
					Next();
				}
				else
					throw new FormularException(tok.Position, tok.Length, "Operant erwartet.");

				// add prefix
				if (negate)
					Append(UnaryInstructionType.Negate);
				if (bitNegate)
					Append(UnaryInstructionType.OnesComplement);

				// faculty as postfix
				if (tok.Type == TokenType.Faculty)
					Append(UnaryInstructionType.Faculity);
			} // proc ParseOperator

			// ** //
			private void ParsePower()
			{
				ParseOperator();

				while (tok.Type == TokenType.Root || tok.Type == TokenType.Power)
				{
					var o = tok.Type;
					Next();

					switch (o)
					{
						case TokenType.Root:
							ParseOperator();
							Append(BinaryInstructionType.Root);
							break;
						case TokenType.Power:
							ParseOperator();
							Append(BinaryInstructionType.Power);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
			} // proc ParsePower

			// * / \ %
			private void ParseMulDiv()
			{
				ParsePower();

				while (tok.Type == TokenType.Star || tok.Type == TokenType.Slash ||
					tok.Type == TokenType.Percent || tok.Type == TokenType.Backshlash ||
					tok.Type == TokenType.Identifier)
				{
					var o = tok.Type;
					if (o == TokenType.Identifier)
						o = TokenType.Star;
					else
						Next();

					switch (o)
					{
						case TokenType.Star: // mul
							ParsePower();
							Append(BinaryInstructionType.Multiply);
							break;
						case TokenType.Slash: // div
							ParsePower();
							Append(BinaryInstructionType.Divide);
							break;
						case TokenType.Percent: // mod
							ParsePower();
							Append(IntegerBinaryInstructionType.IntModulos);
							break;
						case TokenType.Backshlash: // idiv
							ParsePower();
							Append(IntegerBinaryInstructionType.IntDivide);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
			} // proc ParseMulDiv

			// + -
			private void ParsePlusMinus()
			{
				ParseMulDiv();

				while (tok.Type == TokenType.Plus || tok.Type == TokenType.Minus)
				{
					var o = tok.Type;
					Next();

					switch (o)
					{
						case TokenType.Plus:
							ParseMulDiv();
							Append(BinaryInstructionType.Add);
							break;
						case TokenType.Minus:
							ParseMulDiv();
							Append(BinaryInstructionType.Subtract);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
			} // proc ParsePlusMinus

			//// << >>
			private void ParseShift()
			{
				ParsePlusMinus();

				while (tok.Type == TokenType.ShiftLeft || tok.Type == TokenType.ShiftRight)
				{
					var o = tok.Type;
					Next();

					switch (o)
					{
						case TokenType.ShiftRight:
							ParsePlusMinus();
							Append(IntegerBinaryInstructionType.ShiftRight);
							break;
						case TokenType.ShiftLeft:
							ParsePlusMinus();
							Append(IntegerBinaryInstructionType.ShiftLeft);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
			} // proc ParseShift

			// & | ^
			private void ParseBit()
			{
				ParseShift();

				while (tok.Type == TokenType.BitAnd || tok.Type == TokenType.BitOr || tok.Type == TokenType.BitXOr)
				{
					var o = tok.Type;
					Next();

					switch (o)
					{
						case TokenType.BitAnd:
							ParseShift();
							Append(IntegerBinaryInstructionType.BitAnd);
							break;
						case TokenType.BitOr:
							ParseShift();
							Append(IntegerBinaryInstructionType.BitOr);
							break;
						case TokenType.BitXOr:
							ParseShift();
							Append(IntegerBinaryInstructionType.BitXOr);
							break;
						default:
							throw new InvalidOperationException();
					}
				}
			} // proc ParseBit
			
			private void ParseExpr()
			{
				ParseBit();
			} // proc ParseExpr

			public void Parse()
				=> ParseExpr();

			public bool IsEof => tok.Type == TokenType.Eof;

			public int EstimatedStackSize => estimatedStackSize;
			public InstructionBase[] Instructions => instructions.ToArray();

			public int CurrentPosition => tok.Position;
		} // class Parser

		#endregion

		private readonly FormularEnvironment env;
		private readonly string formular;
		private readonly bool useDecimal;

		private readonly int estimatedStackSize;
		private readonly InstructionBase[] instructions;

		#region -- Ctor/Dtor ------------------------------------------------------------

		/// <summary>Initialisiert den Calculator mit Standardwerten.</summary>
		public Formular(FormularEnvironment env, string formular, bool useDecimal = false)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));
			this.formular = formular ?? throw new ArgumentNullException(nameof(formular));
			this.useDecimal = useDecimal;

			(instructions, estimatedStackSize) = Parse(this);
		} // ctor

		private static (InstructionBase[] instr, int estimatedStackSize) Parse(Formular f)
		{
			var p = new Parser(f);
			try
			{
				p.Parse();
				if (!p.IsEof)
					throw new FormularException(p.CurrentPosition, 0, "Operator erwartet.");

				return (p.Instructions, p.EstimatedStackSize); ;
			}
			catch (FormularException e)
			{
				Debug.Print(e.ToString());
				return (null, -1);
			}
		} // proc Parse

		#endregion

		#region -- ScanToken ------------------------------------------------------------

		#region -- class NumberAdd ------------------------------------------------------

		private abstract class NumberAdd
		{
			protected abstract void CoreAdd(int v);

			public bool Add(int v, int scanStart, int scanEnd, ref Token t)
			{
				try
				{
					CoreAdd(v); 
					return true;
				}
				catch (OverflowException)
				{
					return !t.SetToken(TokenType.Error, scanStart, scanEnd, "Zahl zu groß.");
				}
			} // func Add

			public abstract object Value { get; }
		} // class NumberAdd

		#endregion

		#region -- class LongNumberAdd --------------------------------------------------

		private sealed class LongNumberAdd : NumberAdd
		{
			private const long overflowMaskHex = (long)15 << 59;
			private const long overflowMaskOct = (long)7 << 60;
			private const long overflowMaskBin = (long)1 << 63;

			private long number = 0;

			protected override void CoreAdd(int v)
				=> number = checked(number * 10 + v);
			
			public bool AddHex(long v, int scanStart, int scanEnd, ref Token t)
			{
				if ((number & overflowMaskHex) != 0)
					return !t.SetToken(TokenType.Error, scanStart, scanEnd, "Binärüberlauf");

				number = (number << 4) | v;
				return true;
			} // func AddHex

			public bool AddOct(long v, int scanStart, int scanEnd, ref Token t)
			{
				if ((number & overflowMaskOct) != 0)
					return !t.SetToken(TokenType.Error, scanStart, scanEnd, "Binärüberlauf");

				number = (number << 3) | v;
				return true;
			} // func AddOct

			public bool AddBin(long v, int scanStart, int scanEnd, ref Token t)
			{
				if ((number & overflowMaskBin) != 0)
					return !t.SetToken(TokenType.Error, scanStart, scanEnd, "Binärüberlauf");

				number = (number << 1) | v;
				return true;
			} // func AddBin

			public override object Value => number;
		} // class LongNumberAdd

		#endregion

		#region -- class ShortNumberAdd -------------------------------------------------

		private sealed class ShortNumberAdd : NumberAdd
		{
			private short number = 0;

			protected override void CoreAdd(int v)
				=> number = checked((short)(number * 10 + v));

			public override object Value => number;
			public short NativeValue => number;
		} // class ShortNumberAdd

		#endregion

		#region -- class FloatNumberAdd -------------------------------------------------

		private abstract class FloatNumberAdd : NumberAdd
		{
			private bool isExpNeg = false;
			private ShortNumberAdd exp = new ShortNumberAdd();
			
			public void SetNegExponent()
				=> isExpNeg = true;

			public bool AddExp(int v, int scanStart, int scanEnd, ref Token t)
				=> exp.Add(v, scanStart, scanEnd, ref t);

			protected abstract void CoreExponent(bool isNeg, int exp);

			public bool CalcExp(int scanStart, int scanEnd, ref Token t)
			{
				try
				{
					CoreExponent(isExpNeg, exp.NativeValue);
					return true;
				}
				catch (OverflowException)
				{
					t.SetToken(TokenType.Error, scanStart, scanEnd, "Exponentenüberlauf.");
					return false;
				}
			} // func Calcloat

			protected abstract void CoreAddComma(int v);

			public bool AddComma(int v, int scanStart, int scanEnd, ref Token t)
			{
				try
				{
					CoreAddComma(v);
					return true;
				}
				catch (OverflowException)
				{
					t.SetToken(TokenType.Error, scanStart, scanEnd, "Nachkommaüberlauf.");
					return false;
				}
			} // func AddCommand
		} // class FloatNumberAdd

		#endregion

		#region -- class DecimalNumberAdd -----------------------------------------------

		private sealed class DecimalNumberAdd : FloatNumberAdd
		{
			private decimal number;
			private decimal comma = 1m;

			public DecimalNumberAdd(NumberAdd number)
			{
				this.number = Convert.ToDecimal(number.Value);
			} // ctor

			protected override void CoreAdd(int v)
				=> number = number * 10 + v;

			protected override void CoreExponent(bool isNeg, int exp)
				=> number = isNeg
					? checked(number / Convert.ToDecimal(Math.Pow(10, exp)))
					: checked(number * Convert.ToDecimal(Math.Pow(10, exp)));

			protected override void CoreAddComma(int v)
			{
				checked
				{
					comma /= 10;
					number = number + v * comma;
				}
			} // proc CoreAddComma

			public override object Value => number;
		} // class DecimalNumberAdd

		#endregion

		#region -- class DoubleNumberAdd ------------------------------------------------

		private sealed class DoubleNumberAdd : FloatNumberAdd
		{
			private double number;
			private double comma = 1.0;

			public DoubleNumberAdd(NumberAdd number)
			{
				this.number = Convert.ToDouble(number.Value);
			} // ctor

			protected override void CoreAdd(int v)
				=> number = number * 10 + v;

			protected override void CoreExponent(bool isNeg, int exp)
				=> number = isNeg
					? checked(number / Math.Pow(10, exp))
					: checked(number * Math.Pow(10, exp));

			protected override void CoreAddComma(int v)
			{
				checked
				{
					comma /= 10;
					number = number + v * comma;
				}
			} // proc CoreAddComma

			public override object Value => number;
		} // class DoubleNumberAdd

		#endregion

		/// <summary></summary>
		/// <param name="currentPosition"></param>
		/// <param name="tok"></param>
		/// <returns>always <c>true</c></returns>
		private bool ScanToken(ref int currentPosition, ref Token tok)
		{
			var scanStart = currentPosition;
			var state = 0;

			char GetCurrentChar(int pos)
				=> pos < formular.Length
					? formular[pos]
					: pos == formular.Length
						? '\0'
						: throw new ArgumentOutOfRangeException();

			long GetHex(char c)
			{
				if (c >= '0' && c <= '9')
					return c - '0';
				else if (c >= 'A' && c <= 'F')
					return c - 'A';
				else if (c >= 'a' && c <= 'f')
					return c - 'a';
				else
					return -1;
			} // func GetHex

			var currentNumberAdd = (NumberAdd)new LongNumberAdd(); // start with long
			var floatNumberAdd = (FloatNumberAdd)null;

			void ConvertToFloat()
			{
				currentNumberAdd =
					floatNumberAdd = useDecimal
					? (FloatNumberAdd)new DecimalNumberAdd(currentNumberAdd)
					: (FloatNumberAdd)new DoubleNumberAdd(currentNumberAdd);
			} // proc ConvertLong

			while (true)
			{
				// get the current char
				var c = GetCurrentChar(currentPosition);
				
				switch (state)
				{
					#region -- State 0-4 --
					case 0:
						if (c == '\0')
							return tok.SetToken(TokenType.Eof, currentPosition, null);
						else if (c == '0')
							state = 100;
						else if (c >= '1' && c <= '9')
						{
							state = 110;
							currentPosition--;
						}
						else if (c == ',')
							state = 120;
						else if (Char.IsLetter(c))
						{
							state = 200;
							currentPosition--;
						}
						else if (c == '+')
							return tok.SetToken(TokenType.Plus, scanStart, ++currentPosition, null);
						else if (c == '-')
							return tok.SetToken(TokenType.Minus, scanStart, ++currentPosition, null);
						else if (c == '=')
							return tok.SetToken(TokenType.Equal, scanStart, ++currentPosition, null);
						else if (c == ';')
							return tok.SetToken(TokenType.Semi, scanStart, ++currentPosition, null);
						else if (c == ':')
							return tok.SetToken(TokenType.Colon, scanStart, ++currentPosition, null);
						else if (c == '#')
							return tok.SetToken(TokenType.Raute, scanStart, ++currentPosition, null);
						else if (c == '*')
							state = 1;
						else if (c == '/')
							state = 2;
						else if (c == '\\')
							return tok.SetToken(TokenType.Backshlash, scanStart, ++currentPosition, null);
						else if (c == '%')
							return tok.SetToken(TokenType.Percent, scanStart, ++currentPosition, null);
						else if (c == '&')
							return tok.SetToken(TokenType.BitAnd, scanStart, ++currentPosition, null);
						else if (c == '|')
							return tok.SetToken(TokenType.BitOr, scanStart, ++currentPosition, null);
						else if (c == '^')
							return tok.SetToken(TokenType.BitXOr, scanStart, ++currentPosition, null);
						else if (c == '~')
							return tok.SetToken(TokenType.BitNot, scanStart, ++currentPosition, null);
						else if (c == '!')
							return tok.SetToken(TokenType.Faculty, scanStart, ++currentPosition, null);
						else if (c == '<')
							state = 3;
						else if (c == '>')
							state = 4;
						else if (c == '(')
							return tok.SetToken(TokenType.BracketOpen, scanStart, ++currentPosition, null);
						else if (c == ')')
							return tok.SetToken(TokenType.BracketClose, scanStart, ++currentPosition, null);
						else if (c == '\n' || c == '\r' || c == ' ' || c == '\t') // skip whitespaces
							scanStart = currentPosition + 1;
						else
							return tok.SetToken(TokenType.Error, currentPosition, "Ungültiges Zeichen.");
						break;
					case 1: // comes from '*'
						return c == '*' 
								? tok.SetToken(TokenType.Power, scanStart, ++currentPosition, null)
								: tok.SetToken(TokenType.Star, scanStart, currentPosition, null);
					case 2: // comes from '/'
						return c == '/'
							? tok.SetToken(TokenType.Root, scanStart, ++currentPosition, null)
							: tok.SetToken(TokenType.Slash, scanStart, currentPosition, null);
					case 3: // comes from '<'
						return c == '<'
							? tok.SetToken(TokenType.ShiftLeft, scanStart, ++currentPosition, null)
							: tok.SetToken(TokenType.Error, currentPosition, "Unbekannter Ausdruck.");
					case 4: // comes from '>'
						return c == '>'
							? tok.SetToken(TokenType.ShiftRight, scanStart, ++currentPosition, null)
							: tok.SetToken(TokenType.Error, currentPosition, "Unbekannter Ausdruck.");
					#endregion
					#region -- State 100 (Zahl) --
					case 100: // comes from '0'
						if (c == 'x') // scan hex number
							state = 130;
						else if (c == 'o') // scan oct number
							state = 140;
						else if (c == 'b') // scan bin number
							state = 150;
						else if (c == 'e' || c == 'E') // scan double
							state = 115;
						else if (Char.IsDigit(c) || c == '.') // scan int
							state = 110;
						else if (c == ',') // scan dez number
							state = 120;
						else // Es handelt sich um eine 0 (integer)
							return tok.SetToken(TokenType.Number, scanStart, currentPosition, 0L);
						break;
					#region -- Number-Dec --
					case 110: // start scan integer
						if (c >= '0' && c <= '9')
						{
							var v = c - '0';
						ReAdd:
							if (!currentNumberAdd.Add(v, scanStart, currentPosition, ref tok))
							{
								if (currentNumberAdd is LongNumberAdd) // is integer mode switch to dec
								{
									ConvertToFloat();
									goto ReAdd;
								}
								else
									return true;
							}
						}
						else if (c == ',' || c == 'e' || c == 'E')
						{
							if (currentNumberAdd is LongNumberAdd) // currently used long-> convert to comman num
								ConvertToFloat();
							if (c == ',')
								state = 120;
							else
								state = 115;
						}
						else if (c != '.')
							return tok.SetToken(TokenType.Number, scanStart, currentPosition, currentNumberAdd.Value);
						break;
					case 115: // exponent
						if (c == '+')
							state = 116;
						else if (c == '-')
						{
							floatNumberAdd.SetNegExponent();
							state = 116;
						}
						else if (Char.IsDigit(c))
						{
							state = 116;
							currentPosition--;
						}
						else
							return tok.SetToken(TokenType.Error, scanStart, currentPosition, "Exponent erwartet.");
						break;
					case 116: // exponent value
						if (Char.IsDigit(c))
						{
							if (!floatNumberAdd.AddExp(c - '0', scanStart, currentPosition, ref tok))
								return true;
						}
						else
						{
							return floatNumberAdd.CalcExp(scanStart, currentPosition, ref tok)
								? tok.SetToken(TokenType.Number, scanStart, currentPosition, floatNumberAdd.Value)
								: true;
						}
						break;
					case 120: // float part
						if (c >= '0' && c <= '9')
						{
							if (!floatNumberAdd.AddComma(c - '0', scanStart, currentPosition, ref tok))
								return true;
						}
						else if (c == 'e' || c == 'E')
							state = 115;
						else
							return tok.SetToken(TokenType.Number, scanStart, currentPosition, floatNumberAdd.Value);
						break;
					#endregion
					#region -- Number-Hex --
					case 130:
						{
							var v = GetHex(c);
							if (v >= 0)
							{
								if (!((LongNumberAdd)currentNumberAdd).AddHex(v, scanStart, currentPosition, ref tok))
									return true;
							}
							else if (c != '.')
								return tok.SetToken(TokenType.Number, scanStart, currentPosition, currentNumberAdd.Value);
						}
						break;
					#endregion
					#region -- Number-Oct --
					case 140:
						if (c >= '0' && c <= '7')
						{
							if (!((LongNumberAdd)currentNumberAdd).AddOct(c - '0', scanStart, currentPosition, ref tok))
								return true;
						}
						else if (c != '.')
							return tok.SetToken(TokenType.Number, scanStart, currentPosition, currentNumberAdd.Value);
						break;
					#endregion
					#region -- Number-Bin --
					case 150:
						if (c == '0')
						{
							if (!((LongNumberAdd)currentNumberAdd).AddHex(0, scanStart, currentPosition, ref tok))
								return true;
						}
						else if (c == '1')
						{
							if (!((LongNumberAdd)currentNumberAdd).AddHex(1, scanStart, currentPosition, ref tok))
								return true;
						}
						else if (c != '.')
							return tok.SetToken(TokenType.Number, scanStart, currentPosition, currentNumberAdd.Value);
						break;
					#endregion
					#endregion
					#region -- State 200 (Ident) --
					case 200:
						if (!Char.IsLetterOrDigit(c))
							return tok.SetToken(TokenType.Identifier, scanStart, currentPosition, null);
						break;
					#endregion
					default:
						throw new ArgumentException();
				}
				currentPosition++;
			}
		} // func ScanToken

		/// <summary>Gets a part of a formaluar</summary>
		/// <param name="tok"></param>
		/// <returns></returns>
		public string GetFormularPart(ref Token tok)
			=> tok.Length > 0 ? formular.Substring(tok.Position, tok.Length) : String.Empty;

		/// <summary>Get the scanner information.</summary>
		/// <returns></returns>
		public IEnumerable<Token> GetTokens()
		{
			var tok = new Token();
			var currentPosition = 0;

			while (true)
			{
				ScanToken(ref currentPosition, ref tok);
				if (tok.Type == TokenType.Eof)
					yield break;
				yield return tok;
			}
		} // func GetTokens

		#endregion

		#region -- DebugOut, GetResult --------------------------------------------------

		public void DebugOut(TextWriter tw)
		{
			tw.WriteLine("maxstack {0}", estimatedStackSize);
			foreach(var c in instructions)
				tw.WriteLine(c.ToString());
		} // proc DebugOut

		public object GetResult()
		{
			if (!IsValid)
				throw new InvalidOperationException("Formular is not parsed.");

			var stack = new StackMachine(this, estimatedStackSize);
			var l = instructions.Length;
			for (var i = 0; i < l; i++)
				instructions[i].Execute(stack);

			var v = stack.Pop();
			if (!stack.IsEmpty)
				throw new InvalidDataException(); // todo: stack

			return v.Value;
		} // func GetResult

		#endregion

		/// <summary>Returns the orginale formular</summary>
		public string Value => formular;
		/// <summary>Is this formular usable</summary>
		public bool IsValid => instructions != null;
	} // class Formular

	#endregion

	#region -- class FormularParseException ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class FormularException : Exception
	{
		private readonly int position;
		private readonly int length;

		public FormularException(int position, int length, string message)
		  : base(message)
		{
			this.position = position;
			this.length = length;
		} // ctor

		public int Position => position;
		public int Length => length;
	} // class CalculatorException

	#endregion
}