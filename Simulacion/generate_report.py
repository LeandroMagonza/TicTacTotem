import pandas as pd
from collections import Counter
import os

def generate_report(input_csv, output_csv):
    try:
        # Leer el archivo CSV de resultados
        df = pd.read_csv(input_csv)

        # Verificar si las columnas necesarias están presentes
        if 'winner' not in df.columns or 'turns' not in df.columns or 'moves' not in df.columns:
            raise ValueError("El archivo CSV debe contener las columnas 'winner', 'turns' y 'moves'")

        # Convertir turnos a rondas
        df['rounds'] = (df['turns'] // 2) + (df['turns'] % 2)

        # Filtrar las partidas ganadas por cada jugador
        white_wins = df[df['winner'] == 'white'].copy()
        black_wins = df[df['winner'] == 'black'].copy()

        # Total de partidas ganadas por blanco y negro
        total_white_wins = len(white_wins)
        total_black_wins = len(black_wins)
        total_games = len(df)

        # Porcentaje de partidas ganadas por blanco y negro
        white_win_percentage = (total_white_wins / total_games) * 100
        black_win_percentage = (total_black_wins / total_games) * 100

        # Promedio de rondas total
        avg_total_rounds = df['rounds'].mean()

        # Promedio de rondas en partidas ganadas por blanco y negro
        avg_rounds_white_wins = white_wins['rounds'].mean()
        avg_rounds_black_wins = black_wins['rounds'].mean()

        # Promedio de piezas colocadas por el ganador
        def count_pieces(moves):
            return sum(1 for move in moves.split(';') if 'C' in move)

        df['pieces_placed'] = df['moves'].apply(count_pieces)
        white_wins.loc[:, 'pieces_placed'] = white_wins['moves'].apply(count_pieces)
        black_wins.loc[:, 'pieces_placed'] = black_wins['moves'].apply(count_pieces)

        avg_pieces_placed_winner = df[df['winner'] != 'sin terminar']['pieces_placed'].mean()
        avg_pieces_placed_white_wins = white_wins['pieces_placed'].mean()
        avg_pieces_placed_black_wins = black_wins['pieces_placed'].mean()

        # Análisis de jugadas iniciales de blanco
        def first_move(moves):
            return moves.split(';')[0]

        df['first_move'] = df['moves'].apply(first_move)
        initial_move_stats = df.groupby('first_move').agg(
            total_games=('winner', 'count'),
            white_wins=('winner', lambda x: (x == 'white').sum()),
            avg_rounds=('rounds', 'mean')
        )

        initial_move_stats['white_win_percentage'] = (initial_move_stats['white_wins'] / initial_move_stats['total_games']) * 100

        # Distribución de la duración de las partidas
        duration_counter = df['rounds'].value_counts().sort_index()

        # Crear el reporte principal
        report = {
            'Total de partidas ganadas por blanco': total_white_wins,
            'Total de partidas ganadas por negro': total_black_wins,
            'Porcentaje de victorias de blanco': white_win_percentage,
            'Porcentaje de victorias de negro': black_win_percentage,
            'Promedio de piezas colocadas por el ganador': avg_pieces_placed_winner,
            'Promedio de piezas colocadas por blanco cuando gana': avg_pieces_placed_white_wins,
            'Promedio de piezas colocadas por negro cuando gana': avg_pieces_placed_black_wins,
            'Promedio de rondas total': avg_total_rounds,
            'Promedio de rondas en partidas ganadas por blanco': avg_rounds_white_wins,
            'Promedio de rondas en partidas ganadas por negro': avg_rounds_black_wins
        }

        # Guardar el reporte en un archivo CSV
        report_df = pd.DataFrame.from_dict(report, orient='index', columns=['Valor'])
        
        # Crear la tabla de distribución de duración de partidas
        duration_stats = []
        for duration, count in duration_counter.items():
            total_count = count
            white_count = len(df[(df['rounds'] == duration) & (df['winner'] == 'white')])
            black_count = len(df[(df['rounds'] == duration) & (df['winner'] == 'black')])
            white_percentage = (white_count / total_count) * 100 if total_count > 0 else 0
            black_percentage = (black_count / total_count) * 100 if total_count > 0 else 0
            duration_stats.append([duration, total_count, white_percentage, black_percentage])

        duration_df = pd.DataFrame(duration_stats, columns=['Ronda', 'Cantidad de Partidas', '% Ganadas Blanco', '% Ganadas Negro'])

        # Guardar ambos reportes en un archivo CSV
        with open(output_csv, 'w', newline='') as f:
            report_df.to_csv(f)
            f.write('\n')  # Línea en blanco
            duration_df.to_csv(f, index=False)
            f.write('\n')  # Línea en blanco
            initial_move_stats.to_csv(f)

        print(f"Reporte generado exitosamente para {input_csv}.")
    
    except Exception as e:
        print(f"Error al generar el reporte para {input_csv}: {e}")

# Generar reportes para todos los archivos de resultados en el directorio actual
for file in os.listdir('.'):
    if file.startswith('simulation_results_W') and file.endswith('.csv'):
        report_file = file.replace('results', 'report')
        if not os.path.isfile(report_file):
            generate_report(file, report_file)
