using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GRF.FileFormats.LubFormat.Types;
using Utilities.Extension;

namespace GRF.FileFormats.LubFormat.VM {
	public static partial class OpCodes {
		private static readonly Dictionary<string, int> _toIgnore = new Dictionary<string, int>();

		static OpCodes() {
			_toIgnore.Add("(for generator)", 0);
			_toIgnore.Add("(for state)", 0);
			_toIgnore.Add("(for control)", 0);

			_toIgnore.Add("(for limit)", 0);
			_toIgnore.Add("(for index)", 0);
			_toIgnore.Add("(for step)", 0);
		}

		public static void AppendParameters(string functionName, StringBuilder builder, int[] registers, LubFunction function) {
			builder.Append("(");

			if (registers[1] == 1) {
			}
			else if (registers[1] == 0) {
				// Validate the call
				var ins_call = function.Instructions[function.PC - 1] as Call;

				if (ins_call == null) {
					LubErrorHandler.Handle("Expected a function call as the last parameter.", LubSourceError.CodeDecompiler);
				}

				int stackPointer = ins_call.Registers[0];
				bool objectFunction = functionName.Contains(":");

				for (int i = registers[0] + 1 + (objectFunction ? 1 : 0); i <= stackPointer; i++) {
					object toPrint = GetKey(RegOutput(i, function));
					builder.Append(toPrint);

					if (i < stackPointer && toPrint != null) {
						builder.Append(", ");
					}
				}
			}
			else {
				bool objectFunction = functionName.Contains(":");

				for (int i = 1 + (objectFunction ? 1 : 0); i < registers[1]; i++) {
					builder.Append(GetKey(RegOutput(registers[0] + i, function)));

					if (i < registers[1] - 1) {
						builder.Append(", ");
					}
				}
			}

			builder.Append(")");
		}

		public static ILubObject RegOrK(int value, LubFunction function) {
			if (value >= function.Decompiler.Header.ConstantIndexor)
				return function.Constants[value - function.Decompiler.Header.ConstantIndexor];

			var r = function.Stack[value];

			if (r == null) {
				r = GetLocalName(value, function.PC, function);
			}

			return r;
		}

		public static bool IsConstant(int register, LubFunction function) {
			return register >= function.Decompiler.Header.ConstantIndexor;
		}

		public static string GetAccessor(int register, LubFunction function) {
			ILubObject indexer = RegOrK(register, function);

			// Reference
			if (indexer is LubReferenceType) {
				indexer = ((LubReferenceType)indexer).Key;
				return "[" + indexer + "]";
			}

			if (indexer is LubNumber)
				return "[" + indexer + "]";
			if (indexer is LubString) {
				var lubObj = (LubString)indexer;

				if (lubObj.IsValid()) {
					if (IsConstant(register, function) || lubObj.Source == LubSourceType.Constant) {
						return "." + indexer;
					}

					// Reference
					function.Stack[register] = null;
					return "[" + indexer + "]";
				}

				function.Stack[register] = null;

				if (lubObj.Value.Length == 0)
					return "[\"\"]";
				if (lubObj.Value[0] == '\"')
					return "[" + indexer + "]";

				return "[\"" + indexer + "]\"";
			}

			var key = GetKey(RegOrKOutput(register, function));
			//function.Stack[register] = null;
			return "[" + key + "]";
		}

		public static LubReferenceType GetLocalName(int local_number, int line, LubFunction function) {
			int original_line = local_number;

			for (int i = 0; i < function.Debug_LocalVariables.Count && function.Debug_LocalVariables[i].StartLine <= line; i++) {
				//
				if (line <= function.Debug_LocalVariables[i].EndLine) {
					local_number--;

					if (local_number < 0)
						return function.Debug_LocalVariables[i];
				}
			}

			// Alternative!
			line--;
			local_number = original_line;
			local_number++;

			for (int i = 0; i < function.Debug_LocalVariables.Count; i++) {
				//function.Debug_LocalVariables[i].StartLine <= line
				if (line <= function.Debug_LocalVariables[i].EndLine) {
					local_number--;

					if (local_number == 0)
						return function.Debug_LocalVariables[i];
				}
			}

			return null;
		}

		public static ILubObject RegOutput(int value, LubFunction function) {
			ILubObject acces = function.Stack[value];

			if (acces is LubValueType) {
				LubValueType accessor = (LubValueType)acces;

				if (accessor.Source == LubSourceType.Constant && accessor is LubString) {
					return new LubOutput("\"" + accessor + "\"");
				}
			}

			LubReferenceType reference = acces as LubReferenceType;
			if (reference != null) {
				if (function.PC < reference.StartLine) {
					return new LubNull();
				}
			}

			return acces;
		}

		public static ILubObject RA(AbstractInstruction ins, LubFunction function) {
			return function.Stack[ins.Registers[0]];
		}

		public static ILubObject RB(AbstractInstruction ins, LubFunction function) {
			return function.Stack[ins.Registers[1]];
		}

		public static ILubObject RC(AbstractInstruction ins, LubFunction function) {
			return function.Stack[ins.Registers[2]];
		}

		public static ILubObject RKB(AbstractInstruction ins, LubFunction function) {
			return RegOrK(1, function);
		}

		public static ILubObject RegOrKOutput(int value, LubFunction function) {
			ILubObject acces = RegOrK(value, function);

			LubValueType accessor = acces as LubValueType;

			if (accessor != null) {
				if (accessor.Source == LubSourceType.Constant && accessor is LubString) {
					return new LubOutput("\"" + accessor + "\"");
				}
			}

			return acces;
		}

		public static ILubObject GetKey(ILubObject value) {
			if (value is LubReferenceType) {
				return ((LubReferenceType)value).Key;
			}

			LubDictionary lubDictionary = value as LubDictionary;
			if (lubDictionary != null) {
				if (lubDictionary.Count == 0)
					return new LubString("{}");
			}
			return value;
		}

		public static ILubObject GetVal(ILubObject value) {
			return value is LubReferenceType ? ((LubReferenceType)value).Value : value;
		}

		public static T GetKey<T>(ILubObject value) where T : class, ILubObject {
			return value is LubReferenceType ? ((LubReferenceType)value).Key as T : (T)value;
		}

		public static bool ShouldAssign(LubFunction function, int pc, out VarPosition result) {
			// Check both local and global
			var stackData = function.StackResolver.Fetch(pc, function);

			for (int i = 0; i < stackData.Count; i++) {
				ref var data = ref stackData.Results[i];
				var local = data.Debug_LocalVariable;

				if ((data.Flags & (LoopFlag.Parameter | LoopFlag.LoopControl)) == 0 &&
				    !function.IsVariableInstantiated(data.Debug_Index)) {
					result = data;
					return true;
				}

				// Only assign if dumping block variables
				if (_shouldAssign(local, function, data)) {
					result = data;
					return true;
				}
			}

			result = default;
			return false;
		}

		[Flags]
		public enum LoopFlag {
			LoopControl = 1 << 0,
			LoopIterator = 1 << 1,
			Parameter = 1 << 2,
			LoopAssignFirst = 1 << 3,
			LoopAssigned = 1 << 4,
		}

		public struct VarPosition {
			public int Debug_Index;
			public int StackIndex;
			public int LocalOffset;
			public int LoopLength;
			public LoopFlag Flags;

			public LubReferenceType Debug_LocalVariable;

			public bool IsLocalAssign(int pc, LubFunction function) {
				return !Flags.HasFlag(LoopFlag.Parameter) &&
				       !Flags.HasFlag(LoopFlag.LoopControl) &&
				       !function.IsVariableInstantiated(Debug_Index);
			}

			public override string ToString() {
				return Debug_LocalVariable.ToString();
			}
		}

		private static int _fetchCalls;
		private static int _cachedCalls;
		private static int _varPosCreated;
		private static int _listCreated;
		private static int _emptyReturns;
		private static int _unusedVarPositionsList;

		public class StackResolverList {
			public readonly VarPosition[] Results;
			public readonly int Count;

			public StackResolverList(VarPosition[] results, int count) {
				Results = results;
				Count = count;
			}

			public static StackResolverList Empty = new StackResolverList(new VarPosition[0], 0);
		}

		public class StackResolver {
			private readonly Dictionary<int, StackResolverList> _s = new Dictionary<int, StackResolverList>();

			public StackResolverList Fetch(int pc, LubFunction function) {
				_fetchCalls++;
				StackResolverList l;

				if (function.Debug_LocalVariables.Count == 0) {
					_emptyReturns++;
					return StackResolverList.Empty;
				}

				if (_s.TryGetValue(pc, out l)) {
					_cachedCalls++;
					return l;
				}

				VarPosition[] results = new VarPosition[function.Debug_LocalVariables.Count];
				int count = 0;

				int localOffset = 0;

				for (int i = 0; i < function.Debug_LocalVariables.Count && function.Debug_LocalVariables[i].StartLine <= pc; i++) {
					LubReferenceType local = function.Debug_LocalVariables[i];

					if (!local.IsValid(pc)) {
						localOffset++;
						continue;
					}

					var lInfo = LoopInfo.GetLoopInfo(function, local.Key.Value);

					if (lInfo != null) {
						for (int j = 0; j < -lInfo.Start; j++) {
							count--;
						}

						i += lInfo.Start;
						int itStart = -1;

						for (int j = 0; j < lInfo.Length; j++, i++, itStart--) {
							if (j == lInfo.IteratorsStart)
								itStart = lInfo.IteratorsLength;

							results[count++] = new VarPosition {
								Debug_Index = i,
								Debug_LocalVariable = function.Debug_LocalVariables[i],
								StackIndex = i - localOffset,
								LocalOffset = localOffset,
								Flags = LoopFlag.LoopControl | (itStart > 0 ? LoopFlag.LoopIterator : 0) | (j == 0 ? LoopFlag.LoopAssignFirst : 0) | (i < function.NumberOfParametersWithArg ? LoopFlag.Parameter : 0),
								LoopLength = lInfo.Length,
							};
							_varPosCreated++;
						}

						i--;
						continue;
					}

					results[count++] = new VarPosition { Debug_Index = i, Debug_LocalVariable = local, StackIndex = i - localOffset, LocalOffset = localOffset, Flags = (i < function.NumberOfParametersWithArg ? LoopFlag.Parameter : 0) };
					_varPosCreated++;
				}

				if (count <= 0) {
					l = StackResolverList.Empty;
				}
				else {
					l = new StackResolverList(results, count);
					_unusedVarPositionsList += results.Length - count;
					_listCreated++;
				}

				_s[pc] = l;
				return l;
			}

			public void Clear() {
				_s.Clear();
			}
		}

		public sealed class LoopInfo {
			public readonly int Length;
			public readonly int IteratorsLength;
			public readonly int IteratorsStart;
			public readonly int Start;

			// Private constructor forces usage of the static instances
			private LoopInfo(int length, int iteratorsStart, int iteratorsLength, int start) {
				Length = length;
				IteratorsStart = iteratorsStart;
				IteratorsLength = iteratorsLength;
				Start = start;
			}

			public readonly static LoopInfo NumericFor_501 = new LoopInfo(4, 2, 2, 0);
			public readonly static LoopInfo GenericFor_501 = new LoopInfo(5, 3, 2, 0);

			public readonly static LoopInfo NumericFor_500 = new LoopInfo(3, 0, 1, -1);
			public readonly static LoopInfo GenericFor_500 = new LoopInfo(4, 2, 2, 0);

			public static LoopInfo GetLoopInfo(LubFunction function, string value) {
				if (function._decompiler.Header.Version >= 5.1) {
					if (string.Equals(value, "(for generator)", StringComparison.Ordinal))
						return GenericFor_501;
					if (string.Equals(value, "(for index)", StringComparison.Ordinal))
						return NumericFor_501;
				}
				else {
					if (string.Equals(value, "(for generator)", StringComparison.Ordinal))
						return GenericFor_500;
					if (string.Equals(value, "(for index)", StringComparison.Ordinal))
						return NumericFor_500;
					if (string.Equals(value, "(for limit)", StringComparison.Ordinal))
						return NumericFor_500;
				}

				return null;
			}
		}

		public static void VarAssign(StringBuilder builder, LubFunction function) {
			var pc = function.PC;
			var stackData = function.StackResolver.Fetch(pc, function);

			for (int i = 0; i < stackData.Count; i++) {
				ref var data = ref stackData.Results[i];
				var local = data.Debug_LocalVariable;

				if ((data.Flags & LoopFlag.LoopAssigned) == 0) {
					if ((data.Flags & LoopFlag.LoopAssignFirst) != 0) {
						AssignLoopVariables(builder, function, data);
					}

					data.Flags |= LoopFlag.LoopAssigned;
				}

				int stackIndex = data.StackIndex;

				if (_shouldAssign(local, function, data)) {
					builder.AppendIndent(function.BaseIndent);
					var res = RegOutput(stackIndex, function);

					if (GetVal(res) == null)
						res = GetKey(res);

					builder.Append(local.Key + " = " + res);
					builder.AppendLine();
					//local.Value = function.Stack[stackIndex];
					function.Stack[stackIndex] = local;
					function.Stack.SetIsAssigned(stackIndex, false);

					if (stackIndex < function.NumberOfParametersWithArg) {
						// We assigned a local variable
						// We must update the usage of the copied reference
						function.Stack.Internal.Where(p => p is LubReferenceType && Equals(((LubReferenceType)p).Key, local.Key)).
							Where(p => p != local).ToList().ForEach(p => ((LubReferenceType)p).Value = local);
					}
				}
			}
		}

		public static void AssignLoopVariables(StringBuilder builder, LubFunction function, VarPosition data) {
			var baseLocals = data.Debug_Index;
			var localOffset = data.LocalOffset;
			var length = data.LoopLength;

			for (int i = baseLocals; i < baseLocals + length && i < function.Debug_LocalVariables.Count; i++) {
				LubReferenceType local = function.Debug_LocalVariables[i];

				if (_toIgnore.ContainsKey(local.Key.Value))
					continue;

				if (function.PC != local.StartLine)
					continue;

				if (function.Stack[i - localOffset] != local)
					local.LoopValue = function.Stack[i - localOffset];

				function.Stack[i - localOffset] = local;
				function.Instantiated[i] = true;
			}
		}

		private static bool _shouldAssign(LubReferenceType local, LubFunction function, VarPosition data) {
			int stackIndex = data.StackIndex;

			if (!function.Stack.GetIsAssigned(stackIndex))
				return false;

			if (GetVal(function.Stack[stackIndex]) == null && local == function.Stack[stackIndex])
				return false;

			if (!function.IsVariableInstantiated(data.Debug_Index))
				return false;

			return true;
		}

		public static void LocalVarInstantiation(StringBuilder builder, LubFunction function) {
			var pc = function.PC;
			var stackData = function.StackResolver.Fetch(pc, function);

			for (int i = function.NumberOfParametersWithArg; i < stackData.Count; i++) {
				ref var data = ref stackData.Results[i];
				var local = data.Debug_LocalVariable;

				if ((data.Flags & LoopFlag.LoopControl) != 0)
					continue;

				if (function.PC >= local.StartLine
				    && !function.IsVariableInstantiated(data.Debug_Index)
					) {
					var v = GetVal(function.Stack[data.StackIndex]);

					if (v is LubString)
						v = new LubOutput("\"" + v + "\"");

					builder.AppendIndent(function.BaseIndent);

					if (v == null) {
						// Only 5.0 does this
						if (function.Stack[data.StackIndex] != local)
							v = GetKey(function.Stack[data.StackIndex]);
					}

					if (v == null)
						builder.AppendLine("local " + local.Key + " = nil");
					else if (v is LubDictionary) {
						builder.Append("local " + local.Key + " = ");
						v.Print(builder, function.BaseIndent);
						builder.AppendLine();
					}
					else
						builder.AppendLine("local " + local.Key + " = " + v);

					function.Instantiated[data.Debug_Index] = true;
					//local.Value = v == null ? null : function.Stack[data.StackIndex];
					function.Stack[data.StackIndex] = local;
					function.Stack.SetIsAssigned(data.StackIndex, false);
				}
			}
		}
	}
}