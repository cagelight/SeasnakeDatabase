using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SeasnakeDatabase {
	public static class SeasnakeStatic {
		public static byte[] SSDB = new byte[] { 0x53, 0x53, 0x44, 0x42 };
	}
	//NOTE: SEASNAKES ARE EXPECTED TO CREATE THEIR OWN STATIC "FROMFILE" METHODS.
	public interface ISeasnake {
		byte[] GetBytes ();
		void Populate(byte[] content);
		byte SSDBSubformat { get;}
	}

	public class Seasnake0<T> : List<T>, ISeasnake where T : IDatablock, new() {
		public byte SSDBSubformat { get{return 0x00;} }
		public string Description {
			get {return description;}
			set {description = value == null ? String.Empty : (value.Length > 255 ? value.Substring(0, 255) : value);}
		}
		protected string description;
		public Seasnake0(string shortdescription) : base() {
			this.Description = shortdescription;
		}
		public byte[] GetBytes() {
			T Ti = new T ();
			byte[] header = new byte[5 + Description.Length + 2 + (Ti.SizeType == BlockSizeType.Static ? 4 : 0)];
			SeasnakeStatic.SSDB.CopyTo (header, 0);
			header [4] = this.SSDBSubformat;
			header [5] = (byte)Description.Length;
			int index = 6;
			if (header[5] != 0) {
				byte[] shortdesc = Encoding.ASCII.GetBytes (this.Description);
				shortdesc.CopyTo (header, index);
				index += shortdesc.Length;
			}
			header [index] = (byte)Ti.SizeType;
			if (header[index] == 0) {
				BitConverter.GetBytes(Ti.GetStaticSize ()).CopyTo(header, ++index);
			}

			List<byte[]> bodyblocks = new List<byte[]> (this.Select ((x) => x.GetBlock ()));
			byte[] body = new byte[header.Length + bodyblocks.Sum((x) => x.Length) + (bodyblocks.Count * (int)Ti.SizeType)];
			int dataindex = 0;
			Buffer.BlockCopy (header, 0, body, dataindex, header.Length);
			dataindex += header.Length;
			foreach(byte[] d in bodyblocks) {
				switch(Ti.SizeType) {
				case BlockSizeType.Static:
					Buffer.BlockCopy (d, 0, body, dataindex, d.Length);
					dataindex += d.Length;
					break;
				case BlockSizeType.Variable8:
					body [dataindex] = (byte)d.Length;
					dataindex++;
					Buffer.BlockCopy (d, 0, body, dataindex, d.Length);
					dataindex += d.Length;
					break;
				case BlockSizeType.Variable16:
					BitConverter.GetBytes ((ushort)d.Length).CopyTo (body, dataindex);
					dataindex += 2;
					Buffer.BlockCopy (d, 0, body, dataindex, d.Length);
					dataindex += d.Length;
					break;
				case BlockSizeType.Variable32:
					BitConverter.GetBytes (d.Length).CopyTo (body, dataindex);
					dataindex += 4;
					Buffer.BlockCopy (d, 0, body, dataindex, d.Length);
					dataindex += d.Length;
					break;
				}
			}
			return body;
		}
		public void Populate(byte[] content) {
			this.Clear ();
			T Ti = new T ();
			using (MemoryStream MS = new MemoryStream(content)) {
				using(BinaryReader BR = new BinaryReader(MS)) {
					//try {
						if (!Enumerable.SequenceEqual(BR.ReadBytes(4), SeasnakeStatic.SSDB))
							throw new ArgumentException("Not a Seasnake Database.");
						if (BR.ReadByte() != this.SSDBSubformat)
							throw new ArgumentException("Not a Seasnake0 Subformat Database");
						int desclength = (int)BR.ReadByte();
						if (desclength > 0) {
							this.Description = Encoding.UTF8.GetString(BR.ReadBytes(desclength));
						} else {
							this.Description = String.Empty;
						}
						if ((BlockSizeType)BR.ReadByte() != Ti.SizeType)
							throw new ArgumentException(String.Format("Datablock Type Mismatch! Expected {0} Size Type.", Ti.SizeType.ToString()));
						if (Ti.SizeType == BlockSizeType.Static && BR.ReadInt32() != Ti.GetStaticSize())
							throw new ArgumentException(String.Format("Datablock Type Mismatch! Expected {0} Static Size Length.", Ti.GetStaticSize()));
						switch(Ti.SizeType) {
						case BlockSizeType.Static:
							if ((content.Length - BR.BaseStream.Position) % Ti.GetStaticSize() != 0)
								throw new ArgumentException("Detected possible data corruption. Length of body is not divisible by static block size.");
							int readit = (content.Length - (int)BR.BaseStream.Position) / Ti.GetStaticSize();
							for (int i = 0; i < readit; ++i) {
								byte[] block = BR.ReadBytes(Ti.GetStaticSize());
								T dblock = new T();
								dblock.Populate(block);
								this.Add(dblock);
							}
							break;
						case BlockSizeType.Variable8:
							while(BR.BaseStream.Position != content.Length) {
								int size = (int)BR.ReadByte();
								byte[] block = BR.ReadBytes(size);
								T dblock = new T();
								dblock.Populate(block);
								this.Add(dblock);
							}
							break;
						case BlockSizeType.Variable16:
							while(BR.BaseStream.Position != content.Length) {
								int size = (int)BR.ReadUInt16();
								byte[] block = BR.ReadBytes(size);
								T dblock = new T();
								dblock.Populate(block);
								this.Add(dblock);
							}
							break;
						case BlockSizeType.Variable32:
							while(BR.BaseStream.Position != content.Length) {
								int size = BR.ReadInt32();
								byte[] block = BR.ReadBytes(size);
								T dblock = new T();
								dblock.Populate(block);
								this.Add(dblock);
							}
							break;
						}
					//} catch (Exception e) {
					//	Console.WriteLine ("Seasnake0 Populate Error: {0}", e);
					//}
				}
			}
		}
	}

	/*
	 * SeasnakeDatabase Format Explanation
	 * Each line in this explanation is the next part of the format, do not separate blocks by newline character.
	 * Lines are precursed by either an R or a C. R means a required block, C means a conditional block, conditions detailed by its description to the right.
	 * 
	 * //HEADER -- Once at the beginning of the file.
	 * R:(String, 4-Bytes)				"SSDB"
	 * R:(1 Byte)						Pre-defined SSDB Subformat. (See Below)
	 * R:(1 Byte)						Length of short description. (0-255 characters)
	 * C:(String, 0-255 Bytes)			Short description. (THIS BLOCK DOES NOT EXIST IF LENGTH OF SHORT DESCRIPTION IS 0)
	 * R:(1 Byte)						Size Type of a Body Datablock. (See Below)
	 * C:(Int32, 4 Bytes)				Size of a static Body Datablock. (THIS BLOCK DOES NOT EXIST IF SIZE TYPE IS NOT STATIC)
	 * 
	 * //BODY -- Repeats for every Datablock Pair.
	 * C:(1, 2, 4 Bytes)				Length of this Body Datablock. (THIS BLOCK DOES NOT EXIST IF SIZE TYPE IS STATIC, SIZE IS DETERMINED BY HEADER)
	 * R:(Var Bytes)					Body Datablock Bytes.
	 * 
	 * //END OF FILE
	 * 
	 */

	/*
	 * Pre-defined SSDB Subformats:
	 * 
	 * //0x00 = Custom Datablock Sequence
	 * 
	 */

	/*
	 * Pre-defined Size Types:
	 * 
	 * //0x00 = Static
	 * //0x01 = Variable, 1 Byte
	 * //0x02 = Variable, 1 Unsigned Short (2 Bytes)
	 * //0x03 = Variable, 1 Signed Integer (4 Bytes)
	 * 
	 */
}

