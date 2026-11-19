namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Synty prefab names the factory uses, best first. Each list falls through to the next entry when a
    /// pack isn't imported. Packs: PolygonGeneric (ships with every pack), Police Station, Town,
    /// Horror Mansion, Dungeon, Horror Carnival.
    /// </summary>
    static class Synty
    {
        public static readonly string[] CrateWood = { "PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Wood_01", "SM_Gen_Prop_Crate_01" };
        public static readonly string[] CrateWood2 = { "PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Wood_03", "SM_Gen_Prop_Crate_02" };
        public static readonly string[] CrateMetal = { "PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Metal_01", "SM_Gen_Prop_Crate_03" };
        public static readonly string[] CrateMetalOpen = { "PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Metal_Open_01", "SM_Gen_Prop_Crate_03" };
        public static readonly string[] BarrelMetal = { "SM_Gen_Prop_Barrel_Metal_01", "PolygonTown/Prefabs/Props/SM_Prop_Barrel_01" };
        public static readonly string[] BarrelMetal2 = { "SM_Gen_Prop_Barrel_Metal_02", "SM_Gen_Prop_Barrel_Metal_01" };
        public static readonly string[] BarrelBroken = { "PolygonDungeon/Prefabs/Props/SM_Prop_Barrel_Broken_01", "SM_Gen_Prop_Barrel_Metal_03" };
        public static readonly string[] Cardboard = { "SM_Gen_Prop_Cardboard_Box_01", "PolygonTown/Prefabs/Props/SM_Prop_CardboardBox_01" };
        public static readonly string[] Cardboard2 = { "SM_Gen_Prop_Cardboard_Box_03", "PolygonTown/Prefabs/Props/SM_Prop_CardboardBox_03" };
        public static readonly string[] BoxPile = { "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Box_Pile_01", "SM_Gen_Prop_Cardboard_Box_Preset_01" };
        public static readonly string[] CardboardJunk = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Cardboard_Junk_01" };
        public static readonly string[] TrashBag = { "PolygonTown/Prefabs/Props/SM_Prop_TrashBag_01" };
        public static readonly string[] TrashBag2 = { "PolygonTown/Prefabs/Props/SM_Prop_TrashBag_02" };
        public static readonly string[] Papers = { "SM_Gen_Prop_Papers_01", "PolygonPoliceStation/Prefabs/Signs/SM_Sign_Papers_01" };
        public static readonly string[] Papers2 = { "SM_Gen_Prop_Papers_03", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Papers_02" };
        public static readonly string[] Plank = { "SM_Gen_Prop_Plank_01" };
        public static readonly string[] Plank2 = { "SM_Gen_Prop_Plank_02" };
        public static readonly string[] Workbench = { "PolygonTown/Prefabs/Props/SM_Prop_Workbench_01" };
        public static readonly string[] ToolBoard = { "PolygonTown/Prefabs/Props/SM_Prop_ToolBoard_01" };
        public static readonly string[] ToolCabinet = { "PolygonTown/Prefabs/Props/SM_Prop_ToolCabinet_01" };
        public static readonly string[] WorkShelf = { "PolygonTown/Prefabs/Props/SM_Prop_WorkShelf_01", "SM_Gen_Prop_Shelf_01" };
        public static readonly string[] MetalShelf = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Shelf_01", "SM_Gen_Prop_Shelf_02" };
        public static readonly string[] Toolbox = { "PolygonTown/Prefabs/Items/SM_Item_ToolBox_01" };
        public static readonly string[] Ladder = { "PolygonTown/Prefabs/Props/SM_Prop_Ladder_01", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Ladder_01" };
        public static readonly string[] PowerBox = { "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Power_Box_01", "PolygonPoliceStation/Prefabs/Props/SM_Prop_PowerBoxes_01" };
        public static readonly string[] PowerBox2 = { "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Power_Box_02", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Power_Box_01" };
        public static readonly string[] Generator = { "PolygonHorrorCarnival/Prefabs/Props/SM_Prop_Generator_01" };
        public static readonly string[] Cable = { "PolygonHorrorCarnival/Prefabs/Props/SM_Prop_Cable_01" };
        public static readonly string[] Cobweb = { "PolygonHorrorMansion/Prefabs/Environment/SM_Env_SpiderWeb_01", "PolygonHorrorCarnival/Prefabs/Props/SM_Prop_Cobwebs_01" };
        public static readonly string[] Cobweb2 = { "PolygonHorrorMansion/Prefabs/Environment/SM_Env_SpiderWeb_03", "PolygonHorrorCarnival/Prefabs/Props/SM_Prop_Cobwebs_02" };
        public static readonly string[] Chain = { "PolygonDungeon/Prefabs/Props/SM_Prop_Chain_03", "SM_Gen_Prop_Chain_01" };
        public static readonly string[] Hook = { "SM_Gen_Prop_Hook_01", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Hook_01" };
        public static readonly string[] Rubble = { "PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Brick_Rubble_01", "PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rubble_01" };
        public static readonly string[] Rubble2 = { "PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Brick_Rubble_03", "PolygonDungeon/Prefabs/Environments/Rocks/SM_Env_Rubble_02" };
        public static readonly string[] RubblePlank = { "PolygonDungeon/Prefabs/Environments/Wood/SM_Env_Rubble_Plank_01" };
        public static readonly string[] Pipe = { "SM_Gen_Bld_Pipe_Straight_01" };
        public static readonly string[] Valve = { "SM_Gen_Bld_Pipe_Valve_01" };
        public static readonly string[] Vent = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Vents_Straight_01" };
        public static readonly string[] WallVent = { "PolygonHorrorMansion/Prefabs/Buildings/SM_Bld_Wall_Vent_01", "PolygonTown/Prefabs/Props/SM_Prop_Airvent_01" };
        public static readonly string[] Desk = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Desk_01", "PolygonTown/Prefabs/Props/SM_Prop_Desk_01" };
        public static readonly string[] Desk2 = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Desk_02", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Desk_01" };
        public static readonly string[] OfficeChair = { "PolygonTown/Prefabs/Props/SM_Prop_DeskChair_01", "PolygonPoliceStation/Prefabs/Props/SM_Prop_Chair_01" };
        public static readonly string[] Chair = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Chair_02", "SM_Gen_Prop_Chair_01" };
        public static readonly string[] Table = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Table_01", "SM_Gen_Prop_Table_01" };
        public static readonly string[] FilingCabinet = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Filing_Cabinet_01" };
        public static readonly string[] FilingCabinet2 = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Filing_Cabinet_02", "PolygonPoliceStation/Prefabs/Props/SM_Prop_Filing_Cabinet_01" };
        public static readonly string[] Locker = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Weapon_Locker_01" };
        public static readonly string[] Corkboard = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Corkboard_01" };
        public static readonly string[] Clock = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Clock_01", "SM_Gen_Prop_Clock_01" };
        public static readonly string[] Phone = { "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Phone_Rotary_01", "PolygonPoliceStation/Prefabs/Props/SM_Prop_Phone_01" };
        public static readonly string[] Bench = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Bench_01", "PolygonHorrorMansion/Prefabs/Props/SM_Prop_Bench_01" };
        public static readonly string[] Vending = { "PolygonTown/Prefabs/Props/SM_Prop_VendingMachine_01", "PolygonPoliceStation/Prefabs/Props/SM_Prop_Vending_Machine_01" };
        public static readonly string[] Bin = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Bin_01", "PolygonTown/Prefabs/Props/SM_Prop_RubbishBin_01" };
        public static readonly string[] Dumpster = { "PolygonPoliceStation/Prefabs/Props/SM_Prop_Dumpster_01" };
        public static readonly string[] Truck = { "PolygonTown/Prefabs/Vehicles/SM_Veh_Truck_01" };
        public static readonly string[] TruckDelivery = { "PolygonTown/Prefabs/Vehicles/SM_Veh_Truck_Delivery_01" };
        public static readonly string[] Van = { "PolygonPoliceStation/Prefabs/Vehicles/SM_Veh_Van_01" };
        public static readonly string[] Streetlamp = { "PolygonTown/Prefabs/Props/SM_Prop_Streetlamp_01", "PolygonPoliceStation/Prefabs/Props/SM_Prop_Streetlight_01" };
        public static readonly string[] SecurityLight = { "PolygonTown/Prefabs/Props/SM_Prop_SecurityLights_01" };
        public static readonly string[] TreeDead = { "SM_Gen_Env_Tree_Dead_01" };
        public static readonly string[] TreeDead2 = { "SM_Gen_Env_Tree_Dead_02" };
        public static readonly string[] TreeTwisted = { "PolygonHorrorMansion/Prefabs/Environment/SM_Env_Tree_Large_Twisted_01", "SM_Gen_Env_Tree_Dead_03" };
        public static readonly string[] Bush = { "SM_Gen_Env_Bush_01", "PolygonTown/Prefabs/Environment/SM_Env_Bush_01" };
        public static readonly string[] Bush2 = { "SM_Gen_Env_Bush_03", "PolygonHorrorMansion/Prefabs/Environment/SM_Env_Bushes_02" };
        public static readonly string[] Shrub = { "SM_Gen_Env_Shrub_01" };
        public static readonly string[] GrassTall = { "SM_Gen_Env_Grass_Tall_01", "SM_Gen_Env_Grass_01" };
        public static readonly string[] GrassTall2 = { "SM_Gen_Env_Grass_Tall_03", "SM_Gen_Env_Grass_03" };
        public static readonly string[] Grass = { "SM_Gen_Env_Grass_02", "PolygonTown/Prefabs/Environment/SM_Env_Grass_01" };
        public static readonly string[] Ivy = { "SM_Gen_Env_Ivy_Draped_01", "PolygonHorrorMansion/Prefabs/Environment/SM_Env_Ivy_01" };
        public static readonly string[] Ivy2 = { "SM_Gen_Env_Ivy_03", "PolygonHorrorMansion/Prefabs/Environment/SM_Env_Ivy_03" };
        public static readonly string[] Rock = { "SM_Gen_Env_Rock_Pebbles_01", "PolygonTown/Prefabs/Generic/SM_Generic_Small_Rocks_01" };
        public static readonly string[] Blood = { "SM_Gen_FX_Blood_Splatter_01", "FX_Blood_Splatter_01", "PolygonPoliceStation/Prefabs/FX/FX_BloodSplatter_01" };
    }
}
