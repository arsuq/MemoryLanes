/* This Source Code Form is subject to the terms of the Mozilla Public
   License, v. 2.0. If a copy of the MPL was not distributed with this
   file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace System
{
	public class MemoryLaneSettings : IXmlSerializable
	{
		private MemoryLaneSettings() { NextCapacity = (i) => DefaultCapacity; }

		public MemoryLaneSettings(int defLaneCapacity, int maxLanesCount, MemoryLane.DisposalMode dm)
			: this(defLaneCapacity, maxLanesCount, defLaneCapacity * maxLanesCount, dm) { }

		public MemoryLaneSettings(
			int defLaneCapacity = DEF_LANE_CAPACITY,
			int maxLanesCount = MAX_COUNT,
			long maxTotalBytes = MAX_CAPACITY,
			MemoryLane.DisposalMode dm = MemoryLane.DisposalMode.IDispose)
		{
			if (defLaneCapacity > MIN_CAPACITY && defLaneCapacity < MAX_CAPACITY)
				DefaultCapacity = defLaneCapacity;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid max capacity value.");

			if (maxLanesCount > 0 || maxLanesCount <= MAX_COUNT)
				MaxLanesCount = maxLanesCount;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid lane notNullsCount.");

			if (maxTotalBytes > MIN_CAPACITY)
				MaxTotalAllocatedBytes = maxTotalBytes;
			else throw new MemoryLaneException(
				MemoryLaneException.Code.MissingOrInvalidArgument,
				"Invalid total bytes value.");

			NextCapacity = (i) => DefaultCapacity;
			Disposal = dm;
		}

		/// <summary>
		/// Will be invoked if the MaxLanesCount threshold is reached.
		/// The MemoryCarriage would expect a boolean response indicating whether to swallow the 
		/// exception and return null as fragment or throw MemoryLaneException with code MaxLanesCountReached.
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxLanesCountReached</exception>
		[XmlIgnore]
		public Func<bool> OnMaxLaneReached;

		/// <summary>
		/// A handler for the case of allocating more than MaxTotalAllocatedBytes in all lanes.
		/// Pass true in order to suppress the exception and just receive null as fragment.
		/// </summary>
		/// <exception cref="MemoryLaneException">Code.MaxTotalAllocBytesReached</exception>
		[XmlIgnore]
		public Func<bool> OnMaxTotalBytesReached;

		/// <summary>
		/// If set, the function may specify different than the default capacity based on 
		/// the current number of lanes. By default always returns the DefaultCapacity value.
		/// </summary>
		[XmlIgnore]
		public Func<int, int> NextCapacity;

		public const int MAX_COUNT = 5000;
		public const int MIN_CAPACITY = 1023;
		public const int MAX_CAPACITY = 2_000_000_000;
		public const int DEF_LANE_CAPACITY = 8_000_000;


		/// <summary>
		/// Controls how many full cycles around all lanes should be made and fail to enter the 
		/// lock with the specified awaitMS before creating a new lane.
		/// The default value is 2.
		/// </summary>
		[XmlAttribute]
		public int NoWaitLapsBeforeNewLane = 2;

		/// <summary>
		/// If the allocator fail to find a free slice in any lane, 
		/// a new one will be created with DefaultCapacity bytes in length.
		/// </summary>
		[XmlAttribute]
		public readonly int DefaultCapacity;

		/// <summary>
		/// Can be used with the OnMaxLaneReached handler as an alerting mechanism.
		/// </summary>
		[XmlAttribute]
		public readonly int MaxLanesCount;

		/// <summary>
		/// This is the aggregated capacity in all lanes, not the actual active fragments.
		/// </summary>
		[XmlAttribute]
		public readonly long MaxTotalAllocatedBytes;

		/// <summary>
		/// Will trigger a Dispose() before process exits. True by default.
		/// </summary>
		[XmlAttribute]
		public bool RegisterForProcessExitCleanup = true;

		/// <summary>
		/// Specifies the disposal mode.
		/// </summary>
		[XmlAttribute]
		public readonly MemoryLane.DisposalMode Disposal;

		public XmlSchema GetSchema() => null;

		public void ReadXml(XmlReader reader)
		{
			if (reader.HasAttributes)
			{
				var fd = new Dictionary<string, FieldInfo>();

				foreach (var f in getFields())
					fd.Add(f.Name, f);

				while (reader.MoveToNextAttribute())
				{
					var field = reader.Name;
					if (fd.ContainsKey(field))
					{
						var c = fd[field];

						if (c.FieldType == typeof(int))
							c.SetValue(this, int.Parse(reader.Value));
						else if (c.FieldType == typeof(long))
							c.SetValue(this, long.Parse(reader.Value));
						else if (c.FieldType == typeof(bool))
							c.SetValue(this, bool.Parse(reader.Value));
						else if (c.FieldType == typeof(MemoryLane.DisposalMode))
							c.SetValue(this, Enum.Parse<MemoryLane.DisposalMode>(reader.Value));
					}
				}

				if (DefaultCapacity < MIN_CAPACITY || DefaultCapacity > MAX_CAPACITY)
					throw new MemoryLaneException(
						MemoryLaneException.Code.MissingOrInvalidArgument,
						"Invalid max capacity value.");

				if (MaxLanesCount < 0 || MaxLanesCount > MAX_COUNT)
					throw new MemoryLaneException(
					   MemoryLaneException.Code.MissingOrInvalidArgument,
					   "Invalid lane notNullsCount.");

				if (MaxTotalAllocatedBytes < MIN_CAPACITY)
					throw new MemoryLaneException(
					   MemoryLaneException.Code.MissingOrInvalidArgument,
					   "Invalid total bytes value.");
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			foreach (var f in getFields())
				writer.WriteAttributeString(f.Name, f.GetValue(this).ToString());

			writer.Flush();
		}

		IEnumerable<FieldInfo> getFields()
		{
			var T = typeof(MemoryLaneSettings);
			return T.GetFields().Where(f => Attribute.IsDefined(f, typeof(XmlAttributeAttribute)));
		}
	}
}