using ChessChallenge.API;
using System;
using System.Linq;
using System.Collections.Generic;

public class MyBot : IChessBot {
    private const int MAX_DEPTH = 6;
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 310, 330, 500, 900, 10000 };
    private Dictionary<ulong, int> visitedBoards = new();
    private Square[] whiteCastlingSquares = { new(6, 0), new(3, 0) };
    private Square[] blackCastlingSquares = { new(6, 7), new(3, 7) };

    // int[] maximumAttackingSquares = { 0, 2, 8, };

    // int[] pawnValues = {
    //     0, 0, 0, 0, 0, 0, 0, 0,
    //     0, 0, 0, 0, 0, 0, 0, 0,
    // };

    private class EvaluationNode {
        public int Evaluation;
        public Move Move;
        public List<EvaluationNode> Children = new();

        public int treeNodeCount() {
            int count = 1;
            foreach (EvaluationNode child in Children) {
                count += child.treeNodeCount();
            }
            return count;
        }
    }

    private int Evaluate(Board board, bool isWhite, Square[] castlingSquares) {
        int Evaluation = 0;
        foreach(int i in Enumerable.Range(1, 6)) {
            Evaluation += board.GetPieceList(
                (PieceType) i, isWhite).Count * pieceValues[i];
        }
        Square kingSquare = board.GetKingSquare(isWhite);
        if (castlingSquares.Contains(kingSquare)) Evaluation += 20;
        if (board.HasKingsideCastleRight(isWhite)) Evaluation += 8;
        if (board.HasQueensideCastleRight(isWhite)) Evaluation += 8;
        PieceList knights = board.GetPieceList(PieceType.Knight, isWhite);
        foreach(Piece knight in knights) {
            
        }

        return Evaluation;
    }

    private void Evaluate(
        Board board,
        EvaluationNode root) {

        if (visitedBoards.ContainsKey(board.ZobristKey)) {
            root.Evaluation = visitedBoards[board.ZobristKey];
        } else {
            root.Evaluation += Evaluate(board, true, whiteCastlingSquares);
            root.Evaluation -= Evaluate(board, false, blackCastlingSquares);

            visitedBoards.Add(board.ZobristKey, root.Evaluation);
        }
    }

    private void EvaluateMoves(
        Board board,
        Timer timer,
        int thinkingTimeLimitMs,
        int depth,
        bool isMaximizingPlayer,
        Move[] allMoves,
        EvaluationNode root,
        EvaluationNode? parent = null) {
        
        foreach (Move move in allMoves) {
            EvaluationNode newNode = new();
            board.MakeMove(move);
            if (visitedBoards.ContainsKey(board.ZobristKey)) {
                newNode.Evaluation = visitedBoards[board.ZobristKey];
            } else {
                newNode = Think(
                    board,
                    timer,
                    thinkingTimeLimitMs,
                    depth - 1,
                    !isMaximizingPlayer,
                    root);
            }
            board.UndoMove(move);
            newNode.Move = move;

            root.Children.Add(newNode);
            if (isMaximizingPlayer) {
                if (newNode.Evaluation > root.Evaluation) {
                    root.Evaluation = newNode.Evaluation;
                    root.Move = newNode.Move;
                }
                if (parent != null && newNode.Evaluation > parent.Evaluation + 5) {
                    // Console.WriteLine("Parent non-maximizing player already has a better outcome " + parent.Evaluation);
                    break;
                }
            } else if (newNode.Evaluation < root.Evaluation) {
                root.Evaluation = newNode.Evaluation;
                root.Move = newNode.Move;
                if (parent != null && newNode.Evaluation < parent.Evaluation - 5) {
                    // Console.WriteLine("Parent maximizing player already has a better outcome " + parent.Evaluation);
                    break;
                }
            }

        }
    }

    private EvaluationNode Think(
        Board board,
        Timer timer,
        int thinkingTimeLimitMs,
        int depth,
        bool isMaximizingPlayer,
        EvaluationNode? parent = null) {

        Move[] allMoves = board.GetLegalMoves();

        visitedBoards = visitedBoards ?? new();
        Random rng = new();
        EvaluationNode root = new();

        if (depth == 0 || board.IsDraw() || board.IsInCheckmate()) {
            Evaluate(board, root);
            root.Evaluation += rng.Next(11) - 5;
            return root;
        }

        root.Evaluation = isMaximizingPlayer ? Int16.MinValue: Int16.MaxValue;
        root.Move = allMoves[rng.Next(allMoves.Count())];

        if (isMaximizingPlayer && root.Evaluation < 0) {
            allMoves = board.GetLegalMoves(true);
            int oldEvaluation = root.Evaluation;
            EvaluateMoves(
                board, timer, thinkingTimeLimitMs, depth, isMaximizingPlayer,
                allMoves, root, parent);
            if (root.Evaluation < -5) {
                allMoves = board.GetLegalMoves();
                EvaluateMoves(
                    board, timer, thinkingTimeLimitMs, depth, isMaximizingPlayer,
                    allMoves, root, parent);
            }
        } else if (root.Evaluation > 0) {
            allMoves = board.GetLegalMoves(true);
            int oldEvaluation = root.Evaluation;
            EvaluateMoves(
                board, timer, thinkingTimeLimitMs, depth, isMaximizingPlayer, 
                allMoves, root, parent);
            if (root.Evaluation > 5) {
                allMoves = board.GetLegalMoves();
                EvaluateMoves(
                    board, timer, thinkingTimeLimitMs, depth, isMaximizingPlayer, allMoves, root, parent);
            }
        }

        root.Children.Sort((node1, node2) => node1.Evaluation - node2.Evaluation);

        EvaluationNode nodeToPlay;
        if (isMaximizingPlayer) {
            nodeToPlay = root.Children.Last();
        } else {
            nodeToPlay = root.Children.First();
        }
        root.Evaluation = nodeToPlay.Evaluation;
        root.Move = nodeToPlay.Move;
        if (depth == MAX_DEPTH) {
            Console.WriteLine(" --- NEW MOVE --- ");
            Console.WriteLine("PossibleMoveCount: " + allMoves.Count());
            Console.WriteLine("ChildMoveCount: " + root.Children.Count);
            Console.WriteLine("Lowest Evaluation: " + root.Children.First().Evaluation);
            Console.WriteLine("Highest Evaluation: " + root.Children.Last().Evaluation);
            Console.WriteLine("ChosenEvaluation: " + root.Evaluation);
            Console.WriteLine("Visited Boards: " + visitedBoards.Count);
            Console.WriteLine("Visited Nodes: " + root.treeNodeCount());
            Console.WriteLine(" ---------------- ");
        }
        
        visitedBoards[board.ZobristKey] = root.Evaluation;

        return root;
    }

    public Move Think(Board board, Timer timer) {
        return Think(board, timer, timer.MillisecondsRemaining, MAX_DEPTH, board.IsWhiteToMove).Move;
    }
}
