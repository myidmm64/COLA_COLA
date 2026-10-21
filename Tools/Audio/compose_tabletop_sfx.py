"""Author original tactile game SFX. Only final Resources WAVs/metas are written."""
import numpy as np
import argparse
from compose_pizzeria_bgm import RATE, DEST, write, band_noise


def timeline(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def hit(t, at, frequency, decay=32, gain=1):
    age = np.maximum(0, t-at)
    return (t >= at) * (1-np.exp(-age*1600)) * np.exp(-age*decay) * np.sin(2*np.pi*frequency*age)*gain


def rough(t, low, high, seed):
    return band_noise(len(t)/RATE, low, high, seed)


def finish(name, sound, ceiling=.62, loop=False):
    if not loop:
        edge = min(round(RATE*.008), len(sound)//2)
        sound[:edge] *= np.linspace(0, 1, edge)
        sound[-edge:] *= np.linspace(1, 0, edge)
    write('SFX_'+name+'.wav', sound, ceiling)


def items():
    t = timeline(.48)
    # Ice knocks against a cup, followed by a short cold pour.
    ice = sum(hit(t, a, f, d, g) for a,f,d,g in
              ((.012,1710,38,.8),(.075,2430,48,.5),(.145,1950,42,.65)))
    ice += rough(t,450,1800,101)*.20*np.sin(np.pi*np.clip((t-.06)/.37,0,1))**2
    finish('Item_IceCup', ice)
    # Wrapper snap and an evolving gritty fizz, not a generic click.
    candy = rough(t,1800,6500,102)*(.40*np.exp(-t*90)+.22*np.sin(np.pi*t/.48)**2)
    candy += sum(hit(t,a,1700+i*213,95,.14) for i,a in enumerate((.10,.18,.24,.31)))
    finish('Item_FizzCandy', candy)
    ticket = rough(t,1500,7200,104)*np.exp(-((t-.09)/.075)**2)
    ticket *= .4+.6*np.sin(t*310)**2
    ticket += rough(t,350,2200,105)*np.exp(-((t-.23)/.06)**2)*.32
    finish('Item_QueueTicket', ticket)
    bill = rough(t,600,4000,106)*np.exp(-((t-.16)/.10)**2)*.6
    bill += hit(t,.29,128,32,.65)+rough(t,300,1300,107)*np.exp(-np.maximum(0,t-.29)*65)*(t>=.29)*.30
    finish('Item_Bill', bill)
    order = rough(t,1100,5900,108)*(np.exp(-((t-.07)/.045)**2)+.7*np.exp(-((t-.24)/.055)**2))
    order += hit(t,.32,370,55,.40)
    finish('Item_SpicyOrder', order)
    cap = sum(hit(t,a,f,d,g) for a,f,d,g in ((.014,820,55,.8),(.065,1270,75,.5),(.23,620,38,.6)))
    cap += rough(t,900,3500,109)*np.exp(-((t-.13)/.05)**2)*.20
    finish('Item_SafetyCap', cap)
    bread = rough(t,150,2300,110)*(np.exp(-((t-.11)/.06)**2)+.55*np.exp(-((t-.26)/.09)**2))
    bread += rough(t,2100,6000,111)*np.exp(-((t-.10)/.022)**2)*.25
    finish('Item_PizzaSlice', bread, .52)
    sauce = rough(t,180,1600,112)*np.exp(-((t-.23)/.09)**2)*(.35+.65*np.sin(t*46)**4)
    sauce += hit(t,.016,460,48,.5)+hit(t,.18,180,28,.5)+hit(t,.31,260,35,.3)
    finish('Item_HotSauce', sauce)
    stamp = hit(t,.02,155,32,.9)+hit(t,.19,205,38,.7)
    stamp += rough(t,200,2200,113)*(np.exp(-np.maximum(0,t-.02)*55)*(t>=.02)+.7*np.exp(-np.maximum(0,t-.19)*70)*(t>=.19))*.35
    finish('Item_DoubleStamp', stamp)


def new_items():
    t = timeline(.48)
    opener = hit(t,.04,740,65,.7)+hit(t,.12,1140,50,.35)+rough(t,900,4300,501)*np.exp(-((t-.15)/.025)**2)*.18
    finish('Item_BottleOpener',opener,.48)
    straw = rough(t,350,2100,502)*np.exp(-((t-.16)/.075)**2)*(.4+.6*np.sin(t*70)**2)
    finish('Item_Straw',straw+hit(t,.28,640,45,.35),.42)
    water = rough(t,700,5200,503)*np.exp(-((t-.18)/.09)**2)*(.45+.55*np.sin(t*48)**4)
    finish('Item_SodaWater',water+hit(t,.015,430,44,.25),.48)
    coins = sum(hit(t,a,f,d,g) for a,f,d,g in ((.02,1390,38,.6),(.11,1870,49,.5),(.22,2210,59,.3)))
    finish('Item_CoinCup',coins,.44)
    tongs = hit(t,.05,910,57,.6)+hit(t,.21,690,46,.65)+rough(t,1300,4100,504)*np.exp(-((t-.19)/.035)**2)*.12
    finish('Item_SwapTongs',tongs,.45)


def challenge_items():
    t=timeline(.48)
    packet=rough(t,1600,5800,601)*np.exp(-((t-.13)/.07)**2)
    packet+=rough(t,4500,8000,602)*np.exp(-((t-.27)/.04)**2)*.25
    finish('Item_SaltPacket',packet,.42)
    cold=rough(t,250,1400,603)*np.exp(-((t-.18)/.10)**2)
    cold+=hit(t,.04,980,42,.25)+hit(t,.24,1460,60,.15)
    finish('Item_CoolingPack',cold,.40)
    punch=hit(t,.04,190,45,.8)+hit(t,.09,1150,90,.45)+hit(t,.23,430,55,.3)
    finish('Item_TokenPunch',punch,.46)
    lid=hit(t,.04,640,62,.7)+hit(t,.16,870,70,.45)+rough(t,1300,3100,604)*np.exp(-((t-.27)/.05)**2)*.2
    finish('Item_FreshLid',lid,.42)
    coupon=rough(t,900,4300,605)*np.exp(-((t-.13)/.085)**2)
    coupon+=hit(t,.30,710,58,.2)
    finish('Item_ExchangeCoupon',coupon,.40)


def shaking():
    # Periodic noise and integer-period amplitude modulation make true seamless loops.
    t = timeline(1)
    for name, low, high, seed, gas in (('Low',140,1500,201,.025),('Mid',250,2600,202,.15),('High',450,4400,203,.40)):
        stroke = (.5+.5*np.cos(2*np.pi*4*t))**2
        wet = rough(t,low,high,seed)*(.13+.65*stroke)
        wet += np.sin(2*np.pi*180*t+1.3*np.sin(2*np.pi*4*t))*.11*stroke
        wet += rough(t,2100,7400,seed+10)*gas*(.35+.65*stroke)
        finish('Shake_'+name, wet, .62, loop=True)


def interactions():
    t = timeline(.35)
    finish('Button',hit(t,.008,360,65,.8)+hit(t,.035,690,90,.35)+rough(t,400,2600,301)*np.exp(-t*95)*.35)
    finish('Grip',rough(t,250,2300,302)*np.exp(-((t-.09)/.07)**2)+hit(t,.10,580,55,.20),.45)
    finish('SetDown',hit(t,.016,145,30,.8)+hit(t,.025,440,48,.3)+rough(t,150,1800,303)*np.exp(-t*60)*.5)
    finish('DeliverySlide',rough(t,450,3600,304)*np.sin(np.pi*t/.35)**2,.48)
    finish('DeliveryLand',hit(t,.008,190,32,.8)+hit(t,.019,760,45,.3)+rough(t,250,2400,305)*np.exp(-t*60)*.5)
    finish('DeliveryLandGlass',hit(t,.009,1460,32,.8)+hit(t,.021,2410,40,.4)+hit(t,.012,170,38,.35))
    finish('Blocked',hit(t,.01,180,30,.7)+hit(t,.07,145,35,.5),.46)
    finish('Cart',rough(t,900,4200,306)*np.exp(-((t-.06)/.04)**2)+hit(t,.10,560,70,.25),.44)
    t = timeline(.48)
    for index,name in enumerate(('MenuOpen','MenuClose')):
        finish(name,rough(t,650,4300,310+index)*np.exp(-((t-(.16 if index==0 else .09))/.11)**2)+hit(t,.28,180,45,.25),.47)
    finish('Tokens',sum(hit(t,a,f,36,.55) for a,f in ((.01,1950),(.08,2430),(.15,1680))),.48)
    finish('Safe',rough(t,1800,5500,312)*np.exp(-t*15)*(1-np.exp(-t*75))+.10*hit(t,.01,650,50),.36)
    t = timeline(.7)
    finish('NewBottle',rough(t,350,2500,313)*np.exp(-((t-.15)/.10)**2)+hit(t,.36,170,27,.6)+hit(t,.37,520,42,.25),.53)
    finish('Turn',hit(t,.01,480,22,.6)+hit(t,.04,960,34,.12),.36)
    t = timeline(1.6)
    for name,frequencies in (('Win',(146.8,220,293.6)),('Lose',(110,116.5,164.8))):
        sound = sum(np.sin(2*np.pi*f*t)*np.exp(-t*(2+i*.3))*(1-np.exp(-t*24))/(i+1)
                    for i,f in enumerate(frequencies))
        sound += rough(t,300,1600,320)*.08*np.exp(-t*4)*(1-np.exp(-t*40))
        finish(name,sound,.58)


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--new-items-only',action='store_true')
    parser.add_argument('--challenge-items-only',action='store_true')
    args=parser.parse_args()
    DEST.mkdir(parents=True,exist_ok=True)
    if args.challenge_items_only:
        challenge_items()
    elif not args.new_items_only:
        items()
        shaking()
        interactions()
        new_items()
        challenge_items()
    else:
        new_items()
