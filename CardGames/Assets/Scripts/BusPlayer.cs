
using Unity.Netcode;


public enum RedBlackChoice
{
    None,
    Red,
    Black
}

public class BusPlayer : Player
{
    public bool IsMyTurn { get; private set; }

    public RedBlackChoice CurrentChoice { get; private set; } = RedBlackChoice.None;


    [ClientRpc]
    public override void SetTurnClientRpc(bool isMyTurn)
    {
        IsMyTurn = isMyTurn;

        if (IsOwner && BusUIManager.Instance != null)
        {
            //BusUIManager.Instance.SetPlayerTurn(isMyTurn);
        }
    }
}
