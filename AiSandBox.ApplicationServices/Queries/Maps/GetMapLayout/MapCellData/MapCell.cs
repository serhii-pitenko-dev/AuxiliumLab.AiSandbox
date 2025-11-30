using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout.MapCellData;

public record struct MapCell(ECellType CellType, AgentLayer HeroLayer, AgentLayer EnemyLayer);