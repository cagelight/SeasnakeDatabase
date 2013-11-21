using System;
using System.Collections.Generic;
using System.Linq;

namespace SeasnakeDatabase {
	public class SeasnakeDictionary<K, V> : Dictionary<K, V>, ISeasnake, IDatablock where K : IDatablock, new() where V : IDatablock, new() {
		public byte SSDBSubformat { get{return 0x00;} }
		public BlockSizeType SizeType {get {return BlockSizeType.Variable32;}}
		public SeasnakeDictionary() : base() {
		}
		public SeasnakeDictionary(IDictionary<K, V> dict) : base(dict) {
		}
		public byte[] GetBytes() {
			Seasnake0<DatablockPair<K, V>> SS0 = new Seasnake0<DatablockPair<K, V>> ();
			SS0.AddRange (this.Select((kvp) => new DatablockPair<K, V>(kvp.Key, kvp.Value)));
			return SS0.GetBytes ();
		}
		public void Populate(byte[] content) {
			this.Clear ();
			Seasnake0<DatablockPair<K, V>> SS0 = new Seasnake0<DatablockPair<K, V>> ();
			SS0.Populate (content);
			foreach(DatablockPair<K, V> KVP in SS0) {
				this.Add (KVP.Key, KVP.Value);
			}
		}
		public int GetStaticSize() {
			return 0;
		}
	}
}

