import pandas as pd
import csv
import os
import sys
import copy
import random

# Función para obtener el nombre del archivo basado en las piezas
def get_file_name(white_pieces, black_pieces):
    white_str = ''.join(map(str, sorted(white_pieces)))
    black_str = ''.join(map(str, sorted(black_pieces)))
    return f'simulation_results_W{white_str}_B{black_str}.csv'

# Definimos las piezas iniciales de cada jugador
white_pieces = [1, 2, 2, 3, 3, 5]
black_pieces = [1, 2, 2, 4, 6]

# Tablero vacío inicial
initial_board = [[None for _ in range(3)] for _ in range(3)]

def check_winner(board):
    # Verifica si hay un ganador
    for i in range(3):
        # Filas y columnas
        if all(board[i][j] and board[i][j][1] == 'W' for j in range(3)) or all(board[j][i] and board[j][i][1] == 'W' for j in range(3)):
            return 'W'
        if all(board[i][j] and board[i][j][1] == 'B' for j in range(3)) or all(board[j][i] and board[j][i][1] == 'B' for j in range(3)):
            return 'B'
    
    # Diagonales
    if all(board[i][i] and board[i][i][1] == 'W' for i in range(3)) or all(board[i][2 - i] and board[i][2 - i][1] == 'W' for i in range(3)):
        return 'W'
    if all(board[i][i] and board[i][i][1] == 'B' for i in range(3)) or all(board[i][2 - i] and board[i][2 - i][1] == 'B' for i in range(3)):
        return 'B'
    
    return None

def is_valid_move(board, from_pos, to_pos, piece_value):
    # Verifica si un movimiento es válido
    fx, fy = from_pos
    tx, ty = to_pos
    if 0 <= tx < 3 and 0 <= ty < 3 and (board[tx][ty] is None or board[tx][ty][0] < piece_value):
        return True
    return False

def get_possible_moves(board, pieces, turn):
    moves = []
    # Colocar piezas en posiciones vacías
    for piece in pieces:
        for i in range(3):
            for j in range(3):
                if board[i][j] is None:
                    moves.append(('c', piece, i, j))
    
    # Mover piezas existentes
    for i in range(3):
        for j in range(3):
            if board[i][j] and board[i][j][1] == turn:
                for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                    ni, nj = i + dx, j + dy
                    if is_valid_move(board, (i, j), (ni, nj), board[i][j][0]):
                        moves.append(('m', board[i][j][0], i, j, ni, nj))
    return moves

def apply_move(board, move, turn):
    new_board = copy.deepcopy(board)
    if move[0] == 'c':
        _, piece, x, y = move
        new_board[x][y] = (piece, turn)
    elif move[0] == 'm':
        _, piece, fx, fy, tx, ty = move
        new_board[tx][ty] = (piece, turn)
        new_board[fx][fy] = None
    return new_board

def format_move(move, turn):
    if move[0] == 'c':
        return f'{turn}C{move[1]}({move[2]},{move[3]})'
    elif move[0] == 'm':
        return f'{turn}M{move[1]}({move[2]},{move[3]})-({move[4]},{move[5]})'

def can_opponent_win(board, turn):
    opponent = 'B' if turn == 'W' else 'W'
    possible_moves = get_possible_moves(board, [], opponent)
    for move in possible_moves:
        new_board = apply_move(board, move, opponent)
        if check_winner(new_board) == opponent:
            return True
    return False

def get_best_move(board, possible_moves, turn):
    opponent = 'B' if turn == 'W' else 'W'
    
    # 1. Jugada que me hace ganar
    for move in possible_moves:
        new_board = apply_move(board, move, turn)
        if check_winner(new_board) == turn:
            return move

    # 2. Jugada que no deja que el contrincante gane el próximo turno
    blocking_moves = []
    for move in possible_moves:
        new_board = apply_move(board, move, turn)
        if not can_opponent_win(new_board, opponent):
            blocking_moves.append(move)
    
    if blocking_moves:
        possible_moves = blocking_moves

    # 3. Jugada que me permita hacer línea a mí el turno que viene
    winning_setup_moves = []
    for move in possible_moves:
        new_board = apply_move(board, move, turn)
        if can_win_next_turn(new_board, turn):
            winning_setup_moves.append(move)
    
    if winning_setup_moves:
        return random.choice(winning_setup_moves)

    # 4. Jugada al azar
    return random.choice(possible_moves)

def can_win_next_turn(board, turn):
    possible_moves = get_possible_moves(board, [], turn)
    for move in possible_moves:
        new_board = apply_move(board, move, turn)
        if check_winner(new_board) == turn:
            return True
    return False

def simulate_game(white_pieces, black_pieces, max_turns=20):
    board = copy.deepcopy(initial_board)
    white_pieces_copy = white_pieces.copy()
    black_pieces_copy = black_pieces.copy()
    turn = 'W'
    moves = []

    for _ in range(max_turns):
        possible_moves = get_possible_moves(board, white_pieces_copy if turn == 'W' else black_pieces_copy, turn)
        move = get_best_move(board, possible_moves, turn)

        board = apply_move(board, move, turn)
        moves.append(format_move(move, turn))

        winner = check_winner(board)
        if winner:
            return {
                'winner': 'white' if winner == 'W' else 'black',
                'turns': len(moves),
                'moves': ';'.join(moves)
            }

        if move[0] == 'c':
            if turn == 'W':
                white_pieces_copy.remove(move[1])
            else:
                black_pieces_copy.remove(move[1])
        
        turn = 'B' if turn == 'W' else 'W'

    return {
        'winner': 'sin terminar',
        'turns': max_turns,
        'moves': ';'.join(moves)
    }

def append_to_csv(result, filename):
    file_exists = os.path.isfile(filename)
    with open(filename, 'a', newline='') as csvfile:
        fieldnames = ['winner', 'turns', 'moves']
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
        if not file_exists:
            writer.writeheader()
        writer.writerow(result)

def get_existing_simulations_count(filename):
    if not os.path.isfile(filename):
        return 0
    with open(filename, 'r') as csvfile:
        reader = csv.reader(csvfile)
        return sum(1 for row in reader) - 1  # -1 to exclude header row

if __name__ == '__main__':
    if len(sys.argv) != 2:
        print("Usage: python script.py <num_simulations>")
        sys.exit(1)

    num_simulations = int(sys.argv[1])
    csv_filename = get_file_name(white_pieces, black_pieces)
    
    existing_simulations = get_existing_simulations_count(csv_filename)
    simulations_to_run = num_simulations - existing_simulations

    if simulations_to_run > 0:
        for _ in range(simulations_to_run):
            result = simulate_game(white_pieces, black_pieces)
            if result:
                append_to_csv(result, csv_filename)
    else:
        print(f"Already completed {num_simulations} simulations. No more simulations to run.")
