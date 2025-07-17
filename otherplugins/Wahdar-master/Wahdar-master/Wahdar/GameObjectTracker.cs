using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Numerics;

namespace Wahdar
{
    public enum ObjectCategory
    {
        Unknown,
        Player,
        NPC,
        FriendlyNPC,
        Treasure,
        GatheringPoint,
        Aetheryte,
        EventObject,
        Mount,
        Companion,
        Retainer,
        HousingObject,
        AreaObject,
        CutsceneObject,
        CardStand,
        Ornament,
        IslandSanctuaryObject
    }
    
    public class TrackedObject
    {
        public string ObjectId { get; }
        public string Name { get; }
        public ObjectCategory Category { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
        
        public TrackedObject(string objectId, string name, ObjectCategory category, Vector3 position, float distance)
        {
            ObjectId = objectId;
            Name = name;
            Category = category;
            Position = position;
            Distance = distance;
        }
    }
    
    public class GameObjectTracker
    {
        private readonly IObjectTable _objectTable;
        private readonly IClientState _clientState;
        private readonly Configuration _configuration;
        
        public GameObjectTracker(IObjectTable objectTable, Configuration configuration)
        {
            _objectTable = objectTable;
            _clientState = Plugin.ClientState;
            _configuration = configuration;
        }
        
        public List<TrackedObject> GetTrackedObjects()
        {
            var player = _clientState.LocalPlayer;
            if (player == null)
                return new List<TrackedObject>();
                
            var result = new List<TrackedObject>();
            
            foreach (var obj in _objectTable)
            {
                if (obj == null)
                    continue;
                    
                if (obj.Address == player.Address)
                    continue;
                    
                var distance = Vector3.Distance(player.Position, obj.Position);
                if (distance > _configuration.DetectionRadius)
                    continue;
                    
                var category = GetCategory(obj);
                if (!ShouldDisplay(category))
                    continue;
                    
                if (_configuration.HideUnnamedObjects && string.IsNullOrWhiteSpace(obj.Name.TextValue))
                    continue;
                    
                result.Add(new TrackedObject(
                    obj.Address.ToString(),
                    obj.Name.TextValue,
                    category,
                    obj.Position,
                    distance
                ));
            }
            
            return result;
        }
        
        private ObjectCategory GetCategory(IGameObject obj)
        {
            switch (obj.ObjectKind)
            {
                case ObjectKind.Player:
                    return ObjectCategory.Player;
                    
                case ObjectKind.BattleNpc:
                    return ObjectCategory.NPC;
                    
                case ObjectKind.EventNpc:
                    return ObjectCategory.FriendlyNPC;
                    
                case ObjectKind.Treasure:
                    return ObjectCategory.Treasure;
                    
                case ObjectKind.GatheringPoint:
                    return ObjectCategory.GatheringPoint;
                    
                case ObjectKind.Aetheryte:
                    return ObjectCategory.Aetheryte;
                    
                case ObjectKind.EventObj:
                    return ObjectCategory.EventObject;
                    
                case ObjectKind.MountType:
                    return ObjectCategory.Mount;
                    
                case ObjectKind.Companion:
                    return ObjectCategory.Companion;
                    
                case ObjectKind.Retainer:
                    return ObjectCategory.Retainer;
                    
                case ObjectKind.Housing:
                    return ObjectCategory.HousingObject;
                    
                case ObjectKind.Area:
                    return ObjectCategory.AreaObject;
                    
                case ObjectKind.Cutscene:
                    return ObjectCategory.CutsceneObject;
                    
                case ObjectKind.CardStand:
                    return ObjectCategory.CardStand;
                    
                case ObjectKind.Ornament:
                    return ObjectCategory.Ornament;
                    
                default:
                    if ((byte)obj.ObjectKind == 14)
                        return ObjectCategory.IslandSanctuaryObject;
                    
                    return ObjectCategory.Unknown;
            }
        }
        
        private bool IsFriendlyNpc(IGameObject obj)
        {
            return (obj.SubKind != 0);
        }
        
        private bool ShouldDisplay(ObjectCategory category)
        {
            return category switch
            {
                ObjectCategory.Player => _configuration.ShowPlayers,
                ObjectCategory.NPC or ObjectCategory.FriendlyNPC => _configuration.ShowNPCs,
                ObjectCategory.Treasure => _configuration.ShowTreasure,
                ObjectCategory.GatheringPoint => _configuration.ShowGatheringPoints,
                ObjectCategory.Aetheryte => _configuration.ShowAetherytes,
                ObjectCategory.EventObject => _configuration.ShowEventObjects,
                ObjectCategory.Mount => _configuration.ShowMounts,
                ObjectCategory.Companion => _configuration.ShowCompanions,
                ObjectCategory.Retainer => _configuration.ShowRetainers,
                ObjectCategory.HousingObject => _configuration.ShowHousingObjects,
                ObjectCategory.AreaObject => _configuration.ShowAreaObjects,
                ObjectCategory.CutsceneObject => _configuration.ShowCutsceneObjects,
                ObjectCategory.CardStand => _configuration.ShowCardStands,
                ObjectCategory.Ornament => _configuration.ShowOrnaments,
                ObjectCategory.IslandSanctuaryObject => _configuration.ShowIslandSanctuaryObjects,
                _ => false
            };
        }
    }
} 