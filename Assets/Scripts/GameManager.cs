using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

public class GameManager : MonoBehaviour {
    public static GameManager Instance = null;
    public Cell[,] boardData = new Cell[3,3];
    public Piece selectedPiece;

    public GameObject victoryPanel;
    public GameObject defeatPanel;

    public Image playerTeamIcon;
    public TMP_Text playerScoreText; 
    public int playerScore = 0;
    public Image opponentTeamIcon;
    public TMP_Text opponentScoreText; 
    public int opponentScore = 0;
    public int whoseTurn = 1;

    [FormerlySerializedAs("playerTeam")] public int owner; 
    // Start is called before the first frame update
    private void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
        }
        else {
            Instance = this;
        }

        owner = PhotonNetwork.LocalPlayer.ActorNumber;

        SetHeaderTeamColors();
    }

    private void SetHeaderTeamColors() {
        if (owner == 1) {
            playerTeamIcon.GetComponent<Image>().color = Color.white;
            playerScoreText.color = Color.black;
            opponentTeamIcon.GetComponent<Image>().color = Color.yellow;
            opponentScoreText.color = Color.white;
        }
        else if (owner == 2) {
            playerTeamIcon.GetComponent<Image>().color = Color.yellow;
            playerScoreText.color = Color.white;
            opponentTeamIcon.GetComponent<Image>().color = Color.white;
            opponentScoreText.color = Color.black;
        }
    }

    private void UpdateScores() {
        playerScoreText.text = playerScore.ToString();
        opponentScoreText.text = opponentScore.ToString();
    }


    void Start() {
        
        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.LoadLevel(0);
        }
        InstantiatePieces();
        if (PhotonNetwork.IsMasterClient) {
            InstanceBoard();
        }


    }

    private void InstanceBoard() {
        InstantiateCell(0,0);
        InstantiateCell(0,1);
        InstantiateCell(0,2);
        InstantiateCell(1,0);
        InstantiateCell(1,1);
        InstantiateCell(1,2);
        InstantiateCell(2,0);
        InstantiateCell(2,1);
        InstantiateCell(2,2);
    }
    private void InstantiateCell(int row,int column) {
        var myCustomInitData = new object[] {
            row,
            column
        };
        PhotonNetwork.Instantiate(
            "Prefabs/CELL",
            new Vector3(row*4-4,-.5f,column*4-4), 
            Quaternion.identity,
            0,
            myCustomInitData
        );
    }

    private void InstantiatePieces() {
        if (owner == 1) {
            InstantiatePiece(1,1,new Vector3(-3, 0, 7.5f));
            InstantiatePiece(2,1,new Vector3(0f, 0, 10f));
            InstantiatePiece(2,1,new Vector3(0f, 0, 7.35f));
            InstantiatePiece(3,1,new Vector3(2.6f, 0, 9.5f));
            InstantiatePiece(3,1,new Vector3(2.6f, 0, 7.2f));
            InstantiatePiece(5,1,new Vector3(4.5f, 0, 6.9f));
                
        }
        else if (owner == 2) {
            InstantiatePiece(1,2,new Vector3(7.9f,0,-3f));
            InstantiatePiece(2,2,new Vector3(10.1f,0,0f));
            InstantiatePiece(2,2,new Vector3(7.7f,0,0f));
            InstantiatePiece(4,2,new Vector3(7.25f,0,2.5f));
            InstantiatePiece(6, 2,new Vector3(7f,0,4.5f));
        }
    }

    private void InstantiatePiece(int rank,int team, Vector3 position) {
        var myCustomInitData = new object[] {
            team
        };
        PhotonNetwork.Instantiate(
            "Prefabs/PIECE_"+rank,
            position, 
            Quaternion.identity,
            0,
            myCustomInitData
        );
    }

    public void SelectPiece(Piece piece) {
        if (selectedPiece) {
            selectedPiece.UnmarkAsSelected();
        }
        selectedPiece = piece;
        selectedPiece.MarkAsSelected();
    } 
    
    public void AttemptToPlaceSelectedPieceInCell(Cell cell) {
        if(GameManager.Instance.selectedPiece == null) {
            Debug.Log("No piece selected");
            return;
        }        
        if(!selectedPiece.CanPieceBeMoved()) {
            //this case should happen because we wont let the player select a piece that cant be moved at all
            Debug.Log("Selected piece cant be moved.");
            return;
        }        
        if(!cell.CanPieceBePushed(selectedPiece)) {
            Debug.Log("The cell cant receive the selected piece ");
            return;
        }

        if (selectedPiece.containingCell != null) {
            selectedPiece.containingCell.photonView.RPC("Pop", RpcTarget.Others);
            selectedPiece.containingCell.Pop();
            
        }
        cell.PlacePiece(selectedPiece);
        ClearSelectedPiece();
        
    }

    public void NextTurn() {
        whoseTurn = 3 - whoseTurn;
    }
    public IEnumerator EndMatch(int winner) {
        if (winner == owner) {
            victoryPanel.SetActive(true);
            playerScore++;
        }
        else {
            defeatPanel.SetActive(true);
            opponentScore++;
        }
        UpdateScores();
        Debug.Log(winner+ " won!");
        yield return new WaitForSeconds(3);
        owner = 3 - owner;
        Reset();
        SetHeaderTeamColors();
        victoryPanel.SetActive(false);
        defeatPanel.SetActive(false);
    }

    public int? CheckForWinner() {
        //Vertical Win
        Dictionary<int, float> wins = new Dictionary<int, float>();
        wins[1] = 0;
        wins[2] = 0;
        for (int currentRow = 0; currentRow < boardData.GetLength(0); currentRow++) {
            for (int currentColumn = 0; currentColumn < boardData.GetLength(1); currentColumn++) {
                Cell cellToCheck = boardData[currentRow, currentColumn];
                if (cellToCheck.Peek() != null) {
                    wins[cellToCheck.Peek().owner] += CalculateWinsInCell(cellToCheck);
                }
            }
        }
        
        // this is so in case of a tie, the player who didnt make the tying move wins
        // if both players have 3 in line at the same time, but neither had in the last move
        // it means that the last move uncovered an opponents piece that made 3 in a row, and the opponent wins 
        if (wins[owner] > 0 || wins[3-owner] > 0) {
            wins[whoseTurn] -= 0.5f;
        }
        if (wins[owner] > wins[3-owner]) {
                Debug.Log("Player "+owner+" wins");
                return owner;
        }
        if (wins[owner] < wins[3-owner]) {
                Debug.Log("Player "+ (3 - owner)+" wins");
                return 3-owner;
        } 
        return null;
            
    }

    private int CalculateWinsInCell(Cell cellToCheck) {
        int wins = 0; 
        //Vertical Win
        wins += CalculateWinsByPattern(cellToCheck,1,0,-1,0);       
        //Horizontal Win
        wins += CalculateWinsByPattern(cellToCheck,0,1,0,-1);       
       //Diagonal Win 1
        wins += CalculateWinsByPattern(cellToCheck,-1,-1,1,1);       
        //Diagonal Win 2
        wins += CalculateWinsByPattern(cellToCheck,1,-1,-1,1);

        if (wins>0) {
            Debug.Log("Calculated "+wins+" wins");
        }
        return wins;
    }

    private int CalculateWinsByPattern(Cell cellToCheck,int firstRowDistance,int firstColumnDistance, int lastRowDistance, int lastColumnDistance) {
        int cellToCheckOwner = cellToCheck.Peek().owner;
        int wins = 0;
        
        int firstRowIndex =  cellToCheck.row + firstRowDistance;
        int firstColumnIndex =  cellToCheck.column + firstColumnDistance;
        int lastRowIndex =  cellToCheck.row + lastRowDistance;
        int lastColumnIndex =  cellToCheck.column +lastColumnDistance;
        
        if (
            CheckIfCellExistsAndMatchesOwner(
                firstRowIndex, 
                firstColumnIndex, 
                cellToCheckOwner)
            &&
            CheckIfCellExistsAndMatchesOwner(
                lastRowIndex, 
                lastColumnIndex, 
                cellToCheckOwner)
        ) {
            boardData[firstRowIndex, firstColumnIndex].GetComponent<MeshRenderer>().material = cellToCheck.Peek().baseMaterial;
            boardData[cellToCheck.row, cellToCheck.column].GetComponent<MeshRenderer>().material = cellToCheck.Peek().baseMaterial;
            boardData[lastRowIndex, lastColumnIndex].GetComponent<MeshRenderer>().material = cellToCheck.Peek().baseMaterial;
            wins++;
        }

        return wins;
    }

    bool CheckIfCellExistsAndMatchesOwner(int row, int column,int owner) {
        if (row >= boardData.GetLength(0) || row < 0) {
            return false;
        } 
        if (column >= boardData.GetLength(1) || column < 0) {
            return false;
        }
        if (boardData[row,column].Peek() == null || boardData[row,column].Peek().owner != owner) {
            return false;
        }
        return true;
    }
    public void ClearSelectedPiece() {
        if (selectedPiece) {
            selectedPiece.UnmarkAsSelected();
            selectedPiece = null;
        }
    }

    public void Reset() {
        foreach (Cell cell in boardData) {
            cell.Reset();
        }

        foreach (Piece piece in FindObjectsOfType<Piece>()) {
            if (piece.photonView.IsMine) {
                PhotonNetwork.Destroy(piece.gameObject);
            }
        }

        whoseTurn = 1;
        InstantiatePieces();
    }
}

