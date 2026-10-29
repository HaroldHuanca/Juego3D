"""Preparar bucles sin cortes a partir de los WAV originales del usuario.
Uso: python3 Tools/preparar_ambientes.py <carpeta de descargas>
Los originales no se modifican. Solo requiere la biblioteca estándar de Python.
"""
from pathlib import Path
import array, struct, wave, math, sys, json

def leer(p):
    b=p.read_bytes();offset=12;data=None
    while offset+8<=len(b):
        tag=b[offset:offset+4];n=struct.unpack_from('<I',b,offset+4)[0]
        chunk=b[offset+8:offset+8+n];offset+=8+n+(n%2)
        if tag==b'fmt ':fmt,ch,hz,_,_,bits=struct.unpack_from('<HHIIHH',chunk)
        if tag==b'data':data=chunk
    assert (fmt,bits) in [(1,16),(3,32)] and ch==2 and data
    a=array.array('h' if fmt==1 else 'f');a.frombytes(data)
    if sys.byteorder!='little':a.byteswap()
    return a,ch,hz,32768.0 if fmt==1 else 1.0

def preparar(origen,destino):
    a,ch,hz,scale=leer(origen)
    # Retirar el primer y último segundo; la unión se mezcla durante 4 segundos.
    a=a[hz*ch:-hz*ch]
    rms=math.sqrt(sum((v/scale)**2 for v in a[::32])/len(a[::32]))
    gain=min(0.1/max(rms,1e-9),0.6/max(abs(v/scale) for v in a))
    n=hz*4;overlap=n*ch
    output=array.array('h',(round(max(-1,min(1,v/scale*gain))*32767) for v in a[overlap:-overlap]))
    # Potencia constante entre el final y el principio, conservando la forma
    # de onda en las fronteras (también en el salto del último al primer sample).
    for frame in range(n):
        theta=frame/(n-1)*math.pi/2
        c,s=math.cos(theta),math.sin(theta)
        for channel in range(ch):
            value=(a[len(a)-overlap+frame*ch+channel]*c+a[frame*ch+channel]*s)/scale*gain
            output.append(round(max(-1,min(1,value))*32767))
    destino.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(destino),'wb') as w:
        w.setnchannels(ch);w.setsampwidth(2);w.setframerate(hz)
        if sys.byteorder!='little':output.byteswap()
        w.writeframes(output.tobytes())
    result=dict(archivo=destino.name,segundos=len(output)/ch/hz,ganancia_db=20*math.log10(gain),
                pico=max(abs(v) for v in output)/32768,
                salto_union=max(abs(output[c]-output[-ch+c]) for c in range(ch))/32768,
                crossfade_segundos=4)
    assert result['pico']<1
    print(json.dumps(result));return result

if __name__=='__main__':
    source=Path(sys.argv[1]);root=Path(__file__).resolve().parents[1]
    results=[]
    for pattern,name in [('508187*.wav','Interior/VHS_Loop.wav'),('492139*.wav','Exterior/Grillos_Noche_Loop.wav')]:
        matches=list(source.glob(pattern));assert len(matches)==1
        results.append(preparar(matches[0],root/'Assets/Audio/Ambiente'/name))
    (root/'Logs/AmbientesPreparacion.json').write_text(json.dumps(results,indent=2))
