﻿using System;
using System.Collections.Generic;

namespace Intersect_Server.Classes
{

	public class Player : Entity
	{
		public bool InGame;
        public Client MyClient;
        private bool _sentMap;
        public List<EventIndex> MyEvents = new List<EventIndex>();
        public bool[] Switches;
        public int[] Variables;
        public ItemInstance[] Inventory = new ItemInstance[Constants.MaxInvItems];
        public SpellInstance[] Spells = new SpellInstance[Constants.MaxPlayerSkills];

        //Init
		public Player (int index, Client newClient) : base(index)
		{
            MyClient = newClient;
            Switches = new bool[Constants.SwitchCount];
            Variables = new int[Constants.VariableCount];
            for (int i = 0; i < Constants.MaxInvItems; i++)
            {
                Inventory[i] = new ItemInstance();
            }
            for (int i = 0; i < Constants.MaxPlayerSkills; i++)
            {
                Spells[i] = new SpellInstance();
            }
		}

        //Update
        public void Update()
        {
            if (!InGame) { return; }
            //Check to see if we can spawn events, if already spawned.. update them.
            for (var i = 0; i < Globals.GameMaps[CurrentMap].SurroundingMaps.Count + 1; i++)
            {
                int mapNum;
                if (i == Globals.GameMaps[CurrentMap].SurroundingMaps.Count) { mapNum = CurrentMap; } else { mapNum = Globals.GameMaps[CurrentMap].SurroundingMaps[i]; }
                if (mapNum <= -1) continue;
                foreach (var t in Globals.GameMaps[mapNum].Events)
                {
                    int foundEvent;
                    if (CanSpawnEvent(t, mapNum))
                    {
                        //if event isnt spawned, spawn it
                        foundEvent = EventExists(mapNum, t.SpawnX, t.SpawnY);
                        if (foundEvent == -1)
                        {
                            var pageIndex = SpawnEventPage(t);
                            var tmpEvent = new EventIndex(MyEvents.Count, MyClient)
                            {
                                IsGlobal = false,
                                MapNum = mapNum,
                                PageIndex = pageIndex,
                                SpawnX = t.SpawnX,
                                SpawnY = t.SpawnY
                            };
                            MyEvents.Add(tmpEvent);
                            tmpEvent.EventInstance = new Event(t, pageIndex,
                                MyEvents.Count - 1, mapNum)
                            {
                                Dir = t.MyPages[pageIndex].Graphicy
                            };
                            //Send Spawn Event to Player
                            PacketSender.SendEntityData(MyClient, MyEvents.Count - 1, 1, MyEvents[MyEvents.Count - 1].EventInstance);
                            PacketSender.SendEntityPositionTo(MyClient, MyEvents.Count - 1, 1, MyEvents[MyEvents.Count - 1].EventInstance);

                        }
                        else
                        {
                            MyEvents[foundEvent].Update();
                        }
                    }
                    else
                    {
                        //if event is up and running, destroy itttt
                        foundEvent = EventExists(mapNum, t.SpawnX, t.SpawnY);
                        if (foundEvent <= -1 || foundEvent >= MyEvents.Count) continue;
                        MyEvents[foundEvent] = null;
                        PacketSender.SendEntityLeaveTo(MyClient, foundEvent, 1);
                    }
                }
            }
            for (var i = 0; i < MyEvents.Count; i++)
            {
                if (MyEvents[i] == null) continue;
                var eventFound = false;
                if (MyEvents[i].MapNum != CurrentMap)
                {
                    foreach (var t in Globals.GameMaps[CurrentMap].SurroundingMaps)
                    {
                        if (t == MyEvents[i].MapNum)
                        {
                            eventFound = true;
                        }
                    }
                }
                else
                {
                    eventFound = true;
                }
                if (eventFound) continue;
                MyEvents[i] = null;
                PacketSender.SendEntityLeaveTo(MyClient, i, 1);
            }
        }

        //Spawning/Dying
        private void Respawn()
        {
            Warp(Constants.SpawnMap, Constants.SpawnX, Constants.SpawnY, 1);
        }
        public override void Die()
        {
            base.Die();
            Reset();
            Respawn();
        }

        //Warping
        public override void Warp(int newMap, int newX, int newY)
        {
            Warp(newMap, newX, newY);
        }
        public override void Warp(int newMap, int newX, int newY, int newDir)
        {
            
            CurrentX = newX;
            CurrentY = newY;
            if (newMap != CurrentMap || _sentMap == false)
            {
                Console.WriteLine("Sending warp to player.");
                PacketSender.SendEntityLeave(MyIndex,0,CurrentMap);
                CurrentMap = newMap;
                PacketSender.SendEntityDataToAll(MyIndex, 0, Globals.Entities[MyIndex]);
                PacketSender.SendEntityPositionToAll(MyIndex,0,Globals.Entities[MyIndex]);
                PacketSender.SendMap(MyClient,newMap);
                PacketSender.SendEnterMap(MyClient,newMap);
                _sentMap = true;
                for (var i = 0; i < Globals.Entities.Count; i++)
                {
                    if (i != MyIndex)
                    {
                        PacketSender.SendEntityPositionTo(MyClient, i,0, Globals.Entities[i]);
                    }
                }
            }
            else
            {
                PacketSender.SendEntityPositionToAll(MyIndex,0,Globals.Entities[MyIndex]);
                PacketSender.SendEntityVitals(MyIndex, IsEvent, Globals.Entities[MyIndex]);
                PacketSender.SendEntityStats(MyIndex, IsEvent, Globals.Entities[MyIndex]);
            }
            
        }

        //Inventory
        public bool TryGiveItem(ItemInstance item)
        {
            if (Globals.GameItems[item.ItemNum].Type == (int)Enums.ItemTypes.Consumable || //Allow Stacking on Currency, Consumable, Spell, and item types of none.
                Globals.GameItems[item.ItemNum].Type == (int)Enums.ItemTypes.Currency || 
                Globals.GameItems[item.ItemNum].Type == (int)Enums.ItemTypes.None || 
                Globals.GameItems[item.ItemNum].Type == (int)Enums.ItemTypes.Spell)
            {
                for (int i = 0; i < Constants.MaxInvItems; i++)
                {
                    if (Inventory[i].ItemNum == item.ItemNum)
                    {
                        Inventory[i].ItemVal += item.ItemVal;
                        PacketSender.SendInventoryItemUpdate(MyClient, i);
                        return true;
                    }
                }
            }

            //Either a non stacking item, or we couldn't find the item already existing in the players inventory
            for (int i = 0; i < Constants.MaxInvItems; i++)
            {
                if (Inventory[i].ItemNum == -1){
                    Inventory[i] = item.Clone();
                    PacketSender.SendInventoryItemUpdate(MyClient, i);
                    return true;
                }
            }

            return false;
        }
        public void SwapItems(int item1, int item2)
        {
            ItemInstance tmpInstance = Inventory[item2].Clone();
            Inventory[item2] = Inventory[item1].Clone();
            Inventory[item1] = tmpInstance.Clone();
            PacketSender.SendInventoryItemUpdate(MyClient, item1);
            PacketSender.SendInventoryItemUpdate(MyClient, item2);
        }
        public void DropItems(int slot, int amount)
        {
            if (Inventory[slot].ItemNum > -1)
            {
                if (Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Consumable || //Allow Stacking on Currency, Consumable, Spell, and item types of none.
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Currency ||
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.None ||
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Spell)
                {
                    if (amount >= Inventory[slot].ItemVal)
                    {
                        amount = Inventory[slot].ItemVal;
                    }
                    Globals.GameMaps[CurrentMap].SpawnItem(CurrentX, CurrentY, Inventory[slot], amount);
                    if (amount == Inventory[slot].ItemVal)
                    {
                        Inventory[slot] = new ItemInstance();
                    }
                    else
                    {
                        Inventory[slot].ItemVal -= amount;
                    }
                }
                else
                {
                    Globals.GameMaps[CurrentMap].SpawnItem(CurrentX, CurrentY, Inventory[slot], 1);
                    Inventory[slot] = new ItemInstance();
                }
                PacketSender.SendInventoryItemUpdate(MyClient, slot);
            }
        }
        public void UseItem(int slot)
        {
            //TO be implemented.
            if (Inventory[slot].ItemNum > -1)
            {
                if (Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.None || Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Currency)
                {
                    PacketSender.SendPlayerMsg(MyClient, "You cannot use this item!");
                }
                else
                {
                    //TO DO - CHECK REQUIREMENTS
                    switch (Globals.GameItems[Inventory[slot].ItemNum].Type)
                    {
                        case (int)Enums.ItemTypes.Spell:
                            if (Globals.GameItems[Inventory[slot].ItemNum].Data1 > -1)
                            {
                                TryTeachSpell(new SpellInstance(Globals.GameItems[Inventory[slot].ItemNum].Data1));
                            }
                            TakeItem(slot, 1);
                            break;
                        default:
                            PacketSender.SendPlayerMsg(MyClient, "Use of this item type is not yet implemented.");
                            break;
                    }
                }
            }
            
        }
        public bool TakeItem(int slot, int amount)
        {
            bool returnVal = false;
            if (Inventory[slot].ItemNum > -1)
            {
                if (Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Consumable || //Allow Stacking on Currency, Consumable, Spell, and item types of none.
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Currency ||
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.None ||
                            Globals.GameItems[Inventory[slot].ItemNum].Type == (int)Enums.ItemTypes.Spell)
                {
                    if (amount > Inventory[slot].ItemVal)
                    {
                        amount = Inventory[slot].ItemVal;
                    }
                    else
                    {
                        if (amount == Inventory[slot].ItemVal)
                        {
                            Inventory[slot] = new ItemInstance();
                            returnVal = true;
                        }
                        else
                        {
                            Inventory[slot].ItemVal -= amount;
                            returnVal = true;
                        }
                    }
                }
                else
                {
                    Inventory[slot] = new ItemInstance();
                    returnVal = true;
                }
                PacketSender.SendInventoryItemUpdate(MyClient, slot);
            }
            return returnVal;
        }

        //Skills
        public bool TryTeachSpell(SpellInstance spell)
        {
            if (KnowsSpell(spell.SpellNum)) { return true; }
            for (int i = 0; i < Constants.MaxPlayerSkills; i++)
            {
                if (Spells[i].SpellNum == -1)
                {
                    Spells[i] = spell.Clone();
                    PacketSender.SendPlayerSpellUpdate(MyClient, i);
                    return true;
                }
            }
            return false;
        }
        public bool KnowsSpell(int spellnum)
        {
            for (int i = 0; i < Constants.MaxPlayerSkills; i++)
            {
                if (Spells[i].SpellNum == spellnum) { return true; }
            }
            return false;
        }
        public void SwapSpells(int spell1, int spell2)
        {
            SpellInstance tmpInstance = Spells[spell2].Clone();
            Spells[spell2] = Spells[spell1].Clone();
            Spells[spell1] = tmpInstance.Clone();
            PacketSender.SendPlayerSpellUpdate(MyClient, spell1);
            PacketSender.SendPlayerSpellUpdate(MyClient, spell2);
        }
        public void ForgetSpell(int spell)
        {
            Spells[spell] = new SpellInstance();
            PacketSender.SendPlayerSpellUpdate(MyClient, spell);
        }
        public void UseSpell(int spell)
        {
            PacketSender.SendPlayerMsg(MyClient, "The use of spells is not yet implemented.");
        }


        //Event Processing Methods
        public bool MeetsConditions(EventConditions ec, Event ei){
            if (ec.Switch1 - 1 > -1)
            {
                if (Switches[ec.Switch1 - 1] != ec.Switch1Val)
                {
                    return false;
                }
            }
            if (ec.Switch2 - 1 <= -1) return true;
            if (Switches[ec.Switch2 - 1] == ec.Switch2Val) return true;
            return false;
        }
        private int EventExists(int map, int x, int y)
        {
            for (var i = 0; i < MyEvents.Count; i++)
            {
                if (MyEvents[i] == null) continue;
                if (map == MyEvents[i].MapNum && x == MyEvents[i].SpawnX && y == MyEvents[i].SpawnY)
                {
                    return i;
                }
            }
            return -1;
        }
        public void TryActivateEvent(int eventIndex)
        {
            if (eventIndex <= -1 || eventIndex >= MyEvents.Count) return;
            if (MyEvents[eventIndex] == null) return;
            if (MyEvents[eventIndex].EventInstance.Trigger != 0) return;
            if (!IsEventOneBlockAway(eventIndex)) return;
            if (MyEvents[eventIndex].CallStack.Count != 0) return;
            var newStack = new EventStack { CommandIndex = 0, ListIndex = 0 };
            MyEvents[eventIndex].CallStack.Push(newStack);
        }
        public void RespondToEvent(int eventIndex, int responseId)
        {
            if (eventIndex <= -1 || eventIndex >= MyEvents.Count) return;
            if (MyEvents[eventIndex] == null) return;
            if (MyEvents[eventIndex].CallStack.Count <= 0) return;
            if (MyEvents[eventIndex].CallStack.Peek().WaitingForResponse != 1) return;
            if (MyEvents[eventIndex].CallStack.Peek().ResponseType == 0)
            {
                MyEvents[eventIndex].CallStack.Peek().WaitingForResponse = 0;
            }
            else
            {
                var tmpStack = new EventStack();
                MyEvents[eventIndex].CallStack.Peek().WaitingForResponse = 0;
                tmpStack.CommandIndex = 0;
                tmpStack.ListIndex = MyEvents[eventIndex].EventInstance.BaseEvent.MyPages[MyEvents[eventIndex].PageIndex].CommandLists[MyEvents[eventIndex].CallStack.Peek().ListIndex].Commands[MyEvents[eventIndex].CallStack.Peek().CommandIndex].Ints[responseId - 1];
                MyEvents[eventIndex].CallStack.Peek().CommandIndex++;
                MyEvents[eventIndex].CallStack.Push(tmpStack);
            }
        }
        static bool IsEventOneBlockAway(int eventIndex)
        {

            return true;
        }
        private static int SpawnEventPage(EventStruct myEvent)
        {
            return 0;
        }
        private bool CanSpawnEvent(EventStruct myEvent, int myMap)
        {
            if (MeetsConditions(myEvent.MyPages[0].MyConditions, new Event(myEvent, 0, 0, myMap))) return true;
            return false;
        }
        


	}
}
