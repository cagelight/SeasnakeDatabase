using System;
using System.Collections.Generic;
using System.Text;

namespace SeasnakeDatabase {
	public interface IDatablock {
		BlockSizeType SizeType {get;}
		byte[] GetBlock();
		void Populate(byte[] block);
		/// <summary>
		/// Size of the datablock if static. If NOT static, please return 0.
		/// </summary>
		/// <returns>The size of the datablock as an unsigned integer.</returns>
		int GetStaticSize();
	}
	public enum BlockSizeType {Static = 0, Variable8 = 1, Variable16 = 2, Variable32 = 4} //STATIC DATABLOCKS ARE *ALWAYS* THE SAME NUMBER OF BYTES (SUCH AS INTEGERS), VARIABLE DATABLOCKS CONTAIN DATA WITH VARIABLE LENGTH (SUCH AS STRINGS).
	//STATICS
	public class Int32Datablock : IDatablock {
		public Int32 Value;
		public BlockSizeType SizeType {get {return BlockSizeType.Static;}}
		public Int32Datablock() {
			this.Value = 0;
		}
		public Int32Datablock(int val) {
			this.Value = val;
		}
		public byte[] GetBlock() {
			return BitConverter.GetBytes (this.Value);
		}
		public void Populate(byte [] block) {
			this.Value = BitConverter.ToInt32 (block, 0);
		}
		public int GetStaticSize() {
			return 4;
		}
		public static implicit operator Int32Datablock(int I) {
			return new Int32Datablock (I);
		}
	}
	public class Int16Datablock : IDatablock {
		public Int16 Value;
		public BlockSizeType SizeType {get {return BlockSizeType.Static;}}
		public Int16Datablock() {
			this.Value = 0;
		}
		public Int16Datablock(short val) {
			this.Value = val;
		}
		public byte[] GetBlock() {
			return BitConverter.GetBytes (this.Value);
		}
		public void Populate(byte [] block) {
			this.Value = BitConverter.ToInt16 (block, 0);
		}
		public int GetStaticSize() {
			return 2;
		}
		public static implicit operator Int16Datablock(short I) {
			return new Int16Datablock (I);
		}
	}
	public class ByteDatablock : IDatablock {
		public byte Value;
		public BlockSizeType SizeType {get {return BlockSizeType.Static;}}
		public ByteDatablock() {
			this.Value = 0x00;
		}
		public ByteDatablock(byte val) {
			this.Value = val;
		}
		public byte[] GetBlock() {
			return new byte[] {Value};
		}
		public void Populate(byte [] block) {
			this.Value = block [0];
		}
		public int GetStaticSize() {
			return 1;
		}
		public static implicit operator ByteDatablock(byte I) {
			return new ByteDatablock (I);
		}
	}
	//VARIABLES
	public class ASCIIShortStringDatablock : IDatablock {
		public String Value;
		public BlockSizeType SizeType {get {return BlockSizeType.Variable16;}}
		public ASCIIShortStringDatablock() {
			this.Value = String.Empty;
		}
		public ASCIIShortStringDatablock(string val) {
			this.Value = val;
		}
		public byte[] GetBlock() {
			return Encoding.ASCII.GetBytes (this.Value);
		}
		public void Populate(byte [] block) {
			this.Value = Encoding.ASCII.GetString (block);
		}
		public int GetStaticSize() {
			return 0;
		}
		public static implicit operator ASCIIShortStringDatablock(string I) {
			return new ASCIIShortStringDatablock (I);
		}
	}
	public class UTF8StringDatablock : IDatablock {
		public String Value;
		public BlockSizeType SizeType {get {return BlockSizeType.Variable32;}}
		public UTF8StringDatablock() {
			this.Value = String.Empty;
		}
		public UTF8StringDatablock(string val) {
			this.Value = val;
		}
		public byte[] GetBlock() {
			return Encoding.UTF8.GetBytes (this.Value);
		}
		public void Populate(byte [] block) {
			this.Value = Encoding.UTF8.GetString (block);
		}
		public int GetStaticSize() {
			return 0;
		}
		public static implicit operator UTF8StringDatablock(string I) {
			return new UTF8StringDatablock (I);
		}
	}
	//STRUCTURES
	[Serializable]
	public class DatablockPair<K, V> : IDatablock where K : IDatablock, new() where V : IDatablock, new() {
		public BlockSizeType SizeType {
			get {
				BlockSizeType K = Key.SizeType;
				BlockSizeType V = Value.SizeType;
				if (K == BlockSizeType.Static && V == BlockSizeType.Static) {
					return BlockSizeType.Static;
				} else if ((K == BlockSizeType.Static || V == BlockSizeType.Static) && (K == BlockSizeType.Variable8 || V == BlockSizeType.Variable8)) {
					return BlockSizeType.Variable16;
				} else {
					return BlockSizeType.Variable32;
				}
			}
		}
		public K Key;
		public V Value;
		public DatablockPair() {
			this.Key = new K();
			this.Value = new V();
		}
		public DatablockPair(K Key, V Value) {
			this.Key = Key;
			this.Value = Value;
		}
		public byte[] GetBlock() {
			byte[] keybytes = this.Key.GetBlock ();
			byte[] valuebytes = this.Value.GetBlock ();
			int sizehead = Math.Min ((int) Key.SizeType, (int)Value.SizeType);
			byte[] content = new byte[keybytes.Length + valuebytes.Length + sizehead];
			if (sizehead > 0) {
				int min = Key.SizeType.CompareTo (Value.SizeType) <= 0 ? keybytes.Length : valuebytes.Length;
				switch(sizehead) {
					case 1:
					content [0] = (byte)min;
					break;
					case 2:
					BitConverter.GetBytes ((ushort)min).CopyTo (content, 0);
					break;
					case 4:
					BitConverter.GetBytes (min).CopyTo (content, 0);
					break;
				}
				Buffer.BlockCopy (keybytes, 0, content, sizehead, keybytes.Length);
				Buffer.BlockCopy (valuebytes, 0, content, keybytes.Length + sizehead, valuebytes.Length);
			} else {
				Buffer.BlockCopy (keybytes, 0, content, 0, keybytes.Length);
				Buffer.BlockCopy (valuebytes, 0, content, keybytes.Length, valuebytes.Length);
			}
			return content;
		}
		public void Populate(byte[] block) {
			if (Key.SizeType == BlockSizeType.Static && Value.SizeType == BlockSizeType.Static) {
				if (Key.GetStaticSize () + Value.GetStaticSize () != block.Length)
					throw new ArgumentException ("DatablockPair: Block size is not consistent with expected sizes of Key and Value static lengths.");
				byte[] keyblock = new byte[Key.GetStaticSize()];
				byte[] valueblock = new byte[Value.GetStaticSize()];
				Buffer.BlockCopy (block, 0, keyblock, 0, keyblock.Length);
				Buffer.BlockCopy (block, keyblock.Length, valueblock, 0, valueblock.Length);
				Key.Populate (keyblock);
				Value.Populate (valueblock);
			} else if (Key.SizeType == BlockSizeType.Static) {
				byte[] keyblock = new byte[Key.GetStaticSize()];
				byte[] valueblock = new byte[block.Length - keyblock.Length];
				Buffer.BlockCopy (block, 0, keyblock, 0, keyblock.Length);
				Buffer.BlockCopy (block, keyblock.Length, valueblock, 0, valueblock.Length);
				Key.Populate (keyblock);
				Value.Populate (valueblock);
			} else if (Value.SizeType == BlockSizeType.Static) {
				byte[] valueblock = new byte[Value.GetStaticSize()];
				byte[] keyblock = new byte[block.Length - valueblock.Length];
				Buffer.BlockCopy (block, 0, keyblock, 0, keyblock.Length);
				Buffer.BlockCopy (block, keyblock.Length, valueblock, 0, valueblock.Length);
				Key.Populate (keyblock);
				Value.Populate (valueblock);
			} else {
				byte[] valueblock;
				byte[] keyblock;
				BlockSizeType min = Key.SizeType.CompareTo (Value.SizeType) <= 0 ? Key.SizeType : Value.SizeType;
				switch (min) {
				case BlockSizeType.Variable8:
					if (Key.SizeType > Value.SizeType) {
						valueblock = new byte[(int)block[0]];
						keyblock = new byte[(block.Length - 1) - valueblock.Length];
					} else {
						keyblock = new byte[(int)block[0]];
						valueblock = new byte[(block.Length - 1) - keyblock.Length];
					}
					break;
				case BlockSizeType.Variable16:
					if (Key.SizeType > Value.SizeType) {
						valueblock = new byte[BitConverter.ToUInt16(block, 0)];
						keyblock = new byte[(block.Length - 2) - valueblock.Length];
					} else {
						keyblock = new byte[BitConverter.ToUInt16(block, 0)];
						valueblock = new byte[(block.Length - 2) - keyblock.Length];
					}
					break;
				case BlockSizeType.Variable32:
					if (Key.SizeType > Value.SizeType) {
						valueblock = new byte[BitConverter.ToInt32(block, 0)];
						keyblock = new byte[(block.Length - 4) - valueblock.Length];
					} else {
						keyblock = new byte[BitConverter.ToInt32(block, 0)];
						valueblock = new byte[(block.Length - 4) - keyblock.Length];
					}
					break;
				default:
					break;
				}
				Buffer.BlockCopy (block, (int)min, keyblock, 0, keyblock.Length);
				Buffer.BlockCopy (block, keyblock.Length+(int)min, valueblock, 0, valueblock.Length);
				Key.Populate (keyblock);
				Value.Populate (valueblock);
			}
		} 
		public int GetStaticSize() {
			if (this.SizeType == BlockSizeType.Static) {
				return Key.GetStaticSize () + Value.GetStaticSize ();
			} else {
				return 0;
			}
		}
	}
}

