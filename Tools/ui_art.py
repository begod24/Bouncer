#!/usr/bin/env python3
"""
Bouncer: UI-спрайты в стиле «мел на асфальте» и дудл-иконки для карточек-вкладышей.

Запуск из корня репозитория:  python3 Tools/ui_art.py
Нужны numpy и Pillow. HUD белый — цвет задаётся в Unity (Image.color); иконки вкладышей тёмные.
Иконки мячей (волейбольный, набивной, теннисный, сдутый) рендерит Unity из 3D-моделей, их здесь нет.
Сиды фиксированы: повторный запуск даёт те же картинки.
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI")
# Иконки на вкладышах «напечатаны» тёмными чернилами (ячейка window палитры), HUD — белым мелом.
INK = (43, 54, 70)


def blur(a, radius):
    """Гауссово размытие float-массива (отдельно по осям)."""
    if radius <= 0:
        return a
    r = max(1, int(math.ceil(radius * 3)))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / radius) ** 2)
    k /= k.sum()
    a = np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 1, a)
    return np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 0, a)


class Canvas:
    def __init__(self, w, h, seed):
        self.w, self.h = w, h
        self.rng = np.random.default_rng(seed)
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
        self.x, self.y = xs + 0.5, ys + 0.5
        self.alpha = np.zeros((h, w), np.float32)
        # Неровный край мелового штриха: общий плавный шум на всю картинку.
        self.edge = self.noise(5.0) - 0.5

    def noise(self, cell_x, cell_y=None):
        cell_y = cell_y or cell_x
        gw, gh = int(self.w / cell_x) + 2, int(self.h / cell_y) + 2
        grid = (self.rng.random((gh, gw)) * 255).astype(np.uint8)
        img = Image.fromarray(grid).resize((self.w, self.h), Image.BICUBIC)
        return np.asarray(img, np.float32) / 255.0

    # ---------- штрихи ----------

    def stroke(self, pts, width, closed=False, opacity=1.0, rough=1.2, dash=0):
        pts = [tuple(map(float, p)) for p in pts]
        if closed:
            pts.append(pts[0])
        dist = np.full((self.h, self.w), 1e9, np.float32)
        travelled = 0.0
        for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            length = math.hypot(dx, dy)
            # Пунктир: пропускаем каждый второй отрезок длиной dash.
            if dash and int(travelled // dash) % 2 == 1:
                travelled += length
                continue
            travelled += length
            if length < 1e-6:
                t = np.zeros_like(self.x)
            else:
                t = np.clip(((self.x - x0) * dx + (self.y - y0) * dy) / (length * length), 0.0, 1.0)
            dist = np.minimum(dist, np.hypot(self.x - (x0 + t * dx), self.y - (y0 + t * dy)))
        half = width / 2.0 + self.edge * rough
        self.alpha = np.maximum(self.alpha, np.clip(half - dist + 0.5, 0.0, 1.0) * opacity)

    def fill(self, pts, opacity=1.0, hatch=None, soft=0.8):
        """Заливка многоугольника; hatch=(угол, шаг) — штриховка, как мелом."""
        inside = np.zeros((self.h, self.w), bool)
        n = len(pts)
        for i in range(n):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % n]
            if y0 == y1:
                continue
            crosses = (y0 > self.y) != (y1 > self.y)
            x_at = x0 + (self.y - y0) * (x1 - x0) / (y1 - y0)
            inside ^= crosses & (self.x < x_at)
        cover = blur(inside.astype(np.float32), soft)
        if hatch:
            angle, step = hatch
            a = math.radians(angle)
            phase = (self.x * math.cos(a) + self.y * math.sin(a)) / step * 2 * math.pi
            wobble = (self.noise(12.0) - 0.5) * 2.0
            cover = cover * np.clip((np.sin(phase + wobble) + 0.35) * 1.6, 0.0, 1.0)
        self.alpha = np.maximum(self.alpha, cover * opacity)

    # ---------- фигуры ----------

    @staticmethod
    def circle_pts(cx, cy, r, a0=0.0, a1=360.0, n=72):
        return [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a)))
                for a in np.linspace(a0, a1, n)]

    @staticmethod
    def heart_pts(cx, cy, s, n=90):
        pts = []
        for t in np.linspace(0, 2 * math.pi, n, endpoint=False):
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((cx + x * s, cy - y * s))
        return pts

    @staticmethod
    def round_rect_pts(x0, y0, x1, y1, r, n=8):
        pts = []
        for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
            for a in np.linspace(a0, a0 + 90, n):
                pts.append((cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))))
        return pts

    def arrow_head(self, tip, angle_deg, size, width):
        a = math.radians(angle_deg)
        for side in (-1, 1):
            b = a + math.pi + side * math.radians(28)
            self.stroke([tip, (tip[0] + size * math.cos(b), tip[1] + size * math.sin(b))], width)

    # ---------- запись ----------

    def save(self, name, chalky=True, grain=0.45, pits=0.07, color=(255, 255, 255)):
        a = self.alpha
        if chalky:
            fine = blur(self.rng.random((self.h, self.w)).astype(np.float32), 0.6)
            fine = (fine - fine.min()) / max(1e-6, fine.max() - fine.min())
            streak = self.noise(9.0, 2.0)
            texture = 0.65 * fine + 0.35 * streak
            a = a * np.clip(1.0 - grain + grain * texture * 1.4, 0.0, 1.0)
            a = np.where(self.rng.random((self.h, self.w)) < pits, a * 0.35, a)
        rgba = np.zeros((self.h, self.w, 4), np.uint8)
        rgba[..., :3] = color
        rgba[..., 3] = np.clip(a * 255.0 + 0.5, 0, 255).astype(np.uint8)
        os.makedirs(os.path.dirname(os.path.join(OUT, name)), exist_ok=True)
        Image.fromarray(rgba).save(os.path.join(OUT, name))
        print("wrote", name)


def wobble(pts, amount, seed):
    rng = np.random.default_rng(seed)
    return [(x + rng.uniform(-amount, amount), y + rng.uniform(-amount, amount)) for x, y in pts]


def dense(pts, step=3.0):
    """Ломаная, разбитая на короткие отрезки: пунктир (dash) режет только по отрезкам."""
    out = []
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        out += [(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t) for t in np.linspace(0, 1, n, endpoint=False)]
    out.append(pts[-1])
    return out


def rotate(pts, cx, cy, degrees, dx=0.0, dy=0.0):
    """Поворот вокруг (cx, cy) (в координатах картинки: минус — против часовой) и сдвиг."""
    a = math.radians(degrees)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa + dx, cy + (x - cx) * sa + (y - cy) * ca + dy) for x, y in pts]


# ================================================================ HUD

def hud():
    c = Canvas(128, 128, 1)
    heart = c.heart_pts(64, 60, 3.2)
    c.fill(heart, opacity=0.55, hatch=(35, 7))
    c.stroke(heart, 7, closed=True)
    c.save("HUD_Heart.png")

    c = Canvas(128, 128, 2)
    c.stroke(c.heart_pts(64, 60, 3.2), 5, closed=True, opacity=0.8, dash=9)
    c.save("HUD_HeartEmpty.png")

    c = Canvas(128, 128, 3)
    ball = c.circle_pts(64, 64, 46)
    c.fill(ball, opacity=0.45, hatch=(-30, 7))
    c.stroke(ball, 7, closed=True)
    c.stroke(c.circle_pts(64 + 62, 64 - 58, 88, 118, 166, 24), 6)
    c.save("HUD_Ball.png")

    c = Canvas(128, 128, 4)
    c.stroke(c.circle_pts(64, 64, 46), 5, closed=True, opacity=0.8, dash=10)
    c.save("HUD_BallEmpty.png")

    c = Canvas(256, 64, 5)
    c.stroke(wobble(c.round_rect_pts(8, 9, 248, 55, 10), 1.2, 5), 6, closed=True)
    c.save("HUD_BarFrame.png")

    c = Canvas(256, 64, 6)
    c.fill(c.round_rect_pts(12, 13, 244, 51, 7), opacity=1.0, hatch=(60, 6))
    c.save("HUD_BarFill.png", grain=0.3)

    c = Canvas(128, 128, 7)
    c.stroke(c.circle_pts(64, 64, 34), 7, closed=True)
    for a in (45, 135, 225, 315):
        r0, r1 = 46, 60
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(64 + ca * r1, 64 + sa * r1), (64 + ca * r0, 64 + sa * r0)], 7)
        c.arrow_head((64 + ca * r0, 64 + sa * r0), a + 180, 12, 6)
    c.save("HUD_Catch.png")

    c = Canvas(128, 128, 8)
    c.stroke([(34, 64), (104, 64)], 9)
    c.arrow_head((106, 64), 0, 26, 9)
    for y, x0 in ((40, 18), (88, 18)):
        c.stroke([(x0, y), (x0 + 34, y)], 6, opacity=0.8)
    c.stroke([(8, 64), (22, 64)], 6, opacity=0.8)
    c.save("HUD_Dash.png")

    c = Canvas(512, 40, 9)
    pts = [(12 + i * 4.9, 22 + 5 * math.sin(i * 0.33) + 3 * math.sin(i * 1.7)) for i in range(100)]
    c.stroke(pts, 7)
    c.stroke([(x, y + 8) for x, y in pts[10:70]], 4, opacity=0.6)
    c.save("HUD_Underline.png")

    c = Canvas(128, 128, 10)  # монетка (тиын, тенге): счётчик в HUD и цены в ларьке
    coin = c.circle_pts(64, 64, 46)
    c.fill(coin, opacity=0.35, hatch=(30, 7))
    c.stroke(coin, 7, closed=True)
    c.stroke(c.circle_pts(64, 64, 35), 4, closed=True, opacity=0.6)
    for y in (46, 58):                                   # знак тенге ₸
        c.stroke([(46, y), (82, y)], 7)
    c.stroke([(64, 58), (64, 90)], 7)
    c.save("HUD_Coin.png")

    c = Canvas(128, 128, 11)  # «Крышка от кастрюли»: готовность блока в HUD
    lid = c.circle_pts(64, 64, 46)
    c.fill(lid, opacity=0.3, hatch=(30, 7))
    c.stroke(lid, 7, closed=True)
    c.stroke(c.circle_pts(64, 64, 33), 4, closed=True, opacity=0.6)
    knob = c.circle_pts(64, 64, 11)
    c.fill(knob, opacity=0.9)
    c.stroke(knob, 5, closed=True)
    c.save("HUD_Lid.png")

    c = Canvas(256, 128, 12)  # стрелка мелом на асфальте: выход на следующую арену, метка над ларьком
    c.stroke([(18, 64), (196, 64)], 18)
    c.arrow_head((238, 64), 0, 62, 18)
    for y in (40, 88):
        c.stroke([(26, y), (86, y)], 7, opacity=0.7)
    c.save("Exit_Arrow.png")


# ================================================================ Карточки

def card_frames():
    # Вкладыш: бумажка с зубчатым «обжимом» сверху и снизу, как у обёртки жвачки.
    c = Canvas(360, 520, 20)
    tooth, depth, top, bottom = 18.0, 9.0, 6.0, 514.0
    pts = []
    x = 6.0
    while x < 354.0:
        pts += [(x, top + depth), (min(354.0, x + tooth / 2), top)]
        x += tooth
    pts += [(354.0, top + depth), (354.0, bottom - depth)]
    x = 354.0
    while x > 6.0:
        pts += [(x, bottom - depth), (max(6.0, x - tooth / 2), bottom)]
        x -= tooth
    pts += [(6.0, bottom - depth)]
    c.fill(pts, soft=0.7)
    c.save("Card_Paper.png", chalky=False)

    c = Canvas(320, 320, 21)
    c.fill(c.round_rect_pts(4, 4, 316, 316, 22), soft=0.7)
    c.save("Card_Window.png", chalky=False)

    c = Canvas(400, 560, 22)
    c.stroke(c.round_rect_pts(22, 22, 378, 538, 26), 10, closed=True, rough=0)
    c.alpha = np.clip(blur(c.alpha, 6.0) * 2.2, 0, 1)
    c.save("Card_Glow.png", chalky=False)

    # Рамка редкости по контуру вкладыша (цвет — в Unity): у редких синяя, у золотых золотая.
    c = Canvas(360, 520, 23)
    c.stroke(pts, 12, closed=True, rough=0)
    c.save("Card_RarityFrame.png", chalky=False)


def icons():
    ink = 11

    c = Canvas(256, 256, 30)  # Бумеранг: мяч улетает по дуге и возвращается
    arc = c.circle_pts(128, 136, 78, 205, 505, 90)
    c.stroke(arc, ink)
    end = arc[-1]
    c.arrow_head(end, math.degrees(math.atan2(end[1] - arc[-4][1], end[0] - arc[-4][0])), 30, ink)
    ball = c.circle_pts(56, 104, 26)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Boomerang.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 31)  # Раздвоение: мяч и две расходящиеся стрелки
    ball = c.circle_pts(72, 128, 34)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for tip in ((222, 58), (222, 198)):
        c.stroke([(118, 128), (150, 128), tip], ink)
        c.arrow_head(tip, math.degrees(math.atan2(tip[1] - 128, tip[0] - 150)), 28, ink)
    c.save("Icons/Icon_Split.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 32)  # На резинке: мяч на пружинящей резинке, привязанной к руке
    ball = c.circle_pts(128, 184, 40)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    spring = [(128 + (18 if i % 2 else -18) * (0 < i < 12), 142 - i * 9.5) for i in range(13)]
    c.stroke(spring, 7)
    c.stroke(c.circle_pts(128, 24, 14), 8, closed=True)
    c.save("Icons/Icon_Elastic.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 33)  # Ещё мяч: два мяча и плюс
    for cx, cy in ((96, 152), (148, 132)):
        ball = c.circle_pts(cx, cy, 44)
        c.fill(ball, opacity=0.45, hatch=(30, 8))
        c.stroke(ball, 8, closed=True)
    c.stroke([(206, 40), (206, 96)], 12)
    c.stroke([(178, 68), (234, 68)], 12)
    c.save("Icons/Icon_ExtraBall.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 34)  # Новые кеды: кед сбоку и полоски скорости
    shoe = [(70, 190), (70, 138), (104, 128), (130, 96), (162, 96), (170, 134), (228, 152), (236, 190)]
    c.fill(shoe, opacity=0.35, hatch=(-30, 9))
    c.stroke(shoe, 9, closed=True)
    c.stroke([(64, 202), (240, 202)], 12)
    for i in range(3):
        c.stroke([(136 + i * 12, 110 + i * 8), (152 + i * 12, 116 + i * 8)], 5)
    for y, x0 in ((120, 14), (150, 4), (180, 14)):
        c.stroke([(x0, y), (x0 + 42, y)], 7, opacity=0.85)
    c.save("Icons/Icon_Speed.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 35)  # Цепкие руки: мяч в «ладонях» и искры
    ball = c.circle_pts(128, 132, 38)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke(c.circle_pts(128, 132, 78, 120, 240, 30), ink)
    c.stroke(c.circle_pts(128, 132, 78, -60, 60, 30), ink)
    for a in (-90, -60, -120):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(128 + ca * 92, 132 + sa * 92), (128 + ca * 112, 132 + sa * 112)], 7)
    c.save("Icons/Icon_Catch.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 36)  # Длинные руки: магнит притягивает мяч
    c.stroke(c.circle_pts(84, 128, 52, 90, 270, 40), 26, rough=0.6)
    c.stroke([(84, 76), (132, 76)], 26, rough=0.6)
    c.stroke([(84, 180), (132, 180)], 26, rough=0.6)
    for y in (76, 180):
        c.stroke([(118, y - 13), (118, y + 13)], 5, opacity=0.9)
    ball = c.circle_pts(206, 128, 30)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for x in (146, 160):
        c.stroke(c.circle_pts(x - 60, 128, 60, -22, 22, 10), 5, opacity=0.8)
    c.save("Icons/Icon_Pickup.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 37)  # Бутерброд: хлеб и колбаса
    bread = c.round_rect_pts(40, 120, 216, 196, 26)
    c.fill(bread, opacity=0.35, hatch=(40, 10))
    c.stroke(bread, 9, closed=True)
    sausage = [(64 + 128 * t, 108 - 26 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    sausage += [(192 - 128 * t, 126 - 12 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    c.fill(sausage, opacity=0.55, hatch=(-30, 8))
    c.stroke(sausage, 8, closed=True)
    for x in (96, 128, 160):
        c.stroke(c.circle_pts(x, 104, 6), 4, closed=True, opacity=0.8)
    c.save("Icons/Icon_Sandwich.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 38)  # Попрыгунчик: мяч скачет по асфальту
    c.stroke([(12, 226), (244, 226)], 9)
    fall = [(14 + 90 * t, 222 - 104 * (1 - t * t)) for t in np.linspace(0, 1, 24)]
    hop = [(104 + 96 * t, 222 - 120 * (2 * t - t * t)) for t in np.linspace(0, 0.8, 20)]
    c.stroke(fall + hop[1:], 7, dash=13)
    for tip in ((82, 192), (126, 192), (104, 186)):
        c.stroke([(104 + (tip[0] - 104) * 0.45, 214 + (tip[1] - 214) * 0.45), tip], 6)
    ball = c.circle_pts(204, 84, 30)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Bouncy.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 39)  # Горячая картошка: картошка пышет жаром и вот-вот бахнет
    potato = wobble([(128 + 84 * math.cos(a) + 6 * math.cos(3 * a), 160 + 56 * math.sin(a))
                     for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)], 1.5, 39)
    c.fill(potato, opacity=0.4, hatch=(30, 9))
    c.stroke(potato, 9, closed=True)
    for x, y in ((96, 146), (150, 176), (172, 140)):
        c.stroke(c.circle_pts(x, y, 6, 200, 340, 8), 5)
    for x in (88, 128, 168):
        c.stroke([(x + 9 * math.sin(i * 1.1), 92 - i * 11) for i in range(7)], 7)
    for a in (192, 168, -12, 12):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(128 + ca * 100, 160 + sa * 70), (128 + ca * 122, 160 + sa * 84)], 7)
    c.save("Icons/Icon_HotPotato.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 40)  # Жвачка: мяч тянет липкие нитки от пятна на асфальте
    splat = wobble([(96 + 72 * math.cos(a) * (1 + 0.12 * math.sin(5 * a)), 206 + 26 * math.sin(a) * (1 + 0.12 * math.sin(5 * a)))
                    for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)], 1.5, 40)
    c.fill(splat, opacity=0.45, hatch=(-30, 8))
    c.stroke(splat, 8, closed=True)
    ball = c.circle_pts(186, 66, 34)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    # Нитки тянутся от пятна к мячу и провисают — видно, что липкое.
    for (x0, y0), (x1, y1), sag in (((66, 196), (164, 92), 22), ((102, 190), (180, 100), -12), ((136, 196), (196, 98), 14)):
        length = math.hypot(x1 - x0, y1 - y0)
        nx, ny = (y1 - y0) / length, -(x1 - x0) / length
        c.stroke([(x0 + (x1 - x0) * t + nx * sag * math.sin(math.pi * t), y0 + (y1 - y0) * t + ny * sag * math.sin(math.pi * t))
                  for t in np.linspace(0, 1, 24)], 4)
    c.save("Icons/Icon_Gum.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 41)  # Подкат: кед едет подошвой вперёд, из-под пятки пыль
    shoe = [(70, 190), (70, 138), (104, 128), (130, 96), (162, 96), (170, 134), (228, 152), (236, 190)]
    shoe = rotate(shoe, 150, 150, -20, -28, 12)
    c.fill(shoe, opacity=0.35, hatch=(-30, 9))
    c.stroke(shoe, 9, closed=True)
    c.stroke(rotate([(64, 202), (240, 202)], 150, 150, -20, -28, 12), 12)
    for a in range(0, 360, 45):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        r = 22 if a % 90 == 0 else 15
        c.stroke([(226 + ca * 8, 120 + sa * 8), (226 + ca * r, 120 + sa * r)], 6)
    for y, x0, x1 in ((148, 12, 46), (174, 6, 32)):
        c.stroke([(x0, y), (x1, y)], 7, opacity=0.85)
    for x, y, r in ((30, 226, 11), (44, 238, 8), (14, 236, 7)):
        c.stroke(c.circle_pts(x, y, r), 5, closed=True, opacity=0.85)
    c.stroke([(8, 248), (248, 248)], 7, opacity=0.9)
    c.save("Icons/Icon_Tackle.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 42)  # Хулиганство: кулак — четыре согнутых пальца, поперёк большой, ниже запястье в рукаве
    for i, lift in enumerate((6, 0, 3, 12)):
        finger = c.round_rect_pts(58 + 35 * i, 62 + lift, 93 + 35 * i, 130, 16)
        c.fill(finger, opacity=0.4, hatch=(30, 8))
        c.stroke(finger, 8, closed=True)
    thumb = c.round_rect_pts(52, 130, 172, 164, 16)
    c.fill(thumb, opacity=0.4, hatch=(30, 8))
    c.stroke(thumb, 8, closed=True)
    palm = [(172, 130), (198, 130), (198, 176), (178, 202), (86, 202), (64, 184), (60, 164), (172, 164)]
    c.fill(palm, opacity=0.25, hatch=(30, 8))
    c.stroke([(198, 130), (198, 176), (178, 202), (86, 202), (64, 184), (60, 164)], 8)
    c.stroke([(96, 202), (96, 216)], 8)
    c.stroke([(168, 202), (168, 216)], 8)
    # Рукав олимпийки с тремя полосками уходит за край картинки.
    c.stroke(c.round_rect_pts(74, 216, 190, 276, 8), 8, closed=True)
    for x in (108, 132, 156):
        c.stroke([(x, 226), (x, 256)], 7)
    # Кулак трясётся от злости.
    for x0, y0, x1, y1 in ((40, 70, 24, 58), (34, 100, 14, 98), (216, 70, 232, 58), (222, 100, 242, 98)):
        c.stroke([(x0, y0), (x1, y1)], 7)
    c.save("Icons/Icon_Hooligan.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 43)  # «Замри!»: снежинка над волнами — «море волнуется, замри»
    cx, cy, arm = 128, 100, 72
    for k in range(6):
        a = math.radians(90 + k * 60)
        ca, sa = math.cos(a), math.sin(a)
        c.stroke([(cx, cy), (cx + ca * arm, cy - sa * arm)], 10)
        for f, size in ((0.55, 22), (0.82, 14)):
            px, py = cx + ca * arm * f, cy - sa * arm * f
            for side in (-1, 1):
                b = a + side * math.radians(42)
                c.stroke([(px, py), (px + math.cos(b) * size, py - math.sin(b) * size)], 7)
    for y0 in (200, 230):
        c.stroke([(12 + i * 4, y0 + 9 * math.sin(i * 0.36)) for i in range(59)], 8)
    c.save("Icons/Icon_Freeze.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 44)  # Стеночка: мяч отскакивает от кирпичной стенки и бьёт сильнее
    wall = [(180, 20), (236, 20), (236, 236), (180, 236)]
    c.fill(wall, opacity=0.3, hatch=(45, 10))
    c.stroke(wall, 9, closed=True)
    for row, y in enumerate(range(20, 236, 27)):
        if y > 20:
            c.stroke([(180, y), (236, y)], 5, opacity=0.9)
        for x in ((208,) if row % 2 == 0 else (194, 222)):
            c.stroke([(x, y + 3), (x, min(233, y + 24))], 5, opacity=0.9)
    c.stroke(dense([(58, 194), (172, 110), (70, 52)]), 7, dash=12)
    c.arrow_head((70, 52), math.degrees(math.atan2(52 - 110, 70 - 172)), 26, 8)
    for a in (150, 180, 210):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(172 + ca * 10, 110 + sa * 10), (172 + ca * 26, 110 + sa * 26)], 6)
    c.stroke([(122, 102), (122, 130)], 9)
    c.stroke([(108, 116), (136, 116)], 9)
    ball = c.circle_pts(52, 200, 28)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Wall.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 45)  # Рогатка: рогатка, резинка натянута, мяч в кожанке
    c.stroke([(128, 248), (128, 150)], 18, rough=0.8)
    c.stroke([(128, 156), (74, 64)], 15, rough=0.8)
    c.stroke([(128, 156), (182, 64)], 15, rough=0.8)
    c.stroke([(74, 64), (56, 196)], 6)
    c.stroke([(182, 64), (56, 196)], 6)
    ball = c.circle_pts(52, 204, 24)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for x0, y0 in ((196, 44), (214, 64), (222, 92)):
        c.stroke([(x0, y0), (x0 + 22, y0 - 22)], 6, opacity=0.85)
    c.save("Icons/Icon_Slingshot.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 46)  # Глаз-алмаз: глаз, и мяч по дуге сам заходит в мишень
    upper = [(20 + 120 * t, 92 - 34 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 24)]
    lower = [(140 - 120 * t, 92 + 30 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 24)]
    c.stroke(upper + lower, 8, closed=True)
    iris = c.circle_pts(80, 92, 20)
    c.fill(iris, opacity=0.75)
    c.stroke(iris, 6, closed=True)
    for a in (-120, -90, -60):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(80 + ca * 44, 92 + sa * 50), (80 + ca * 58, 92 + sa * 64)], 6)
    curve = [((1 - t) ** 2 * 118 + 2 * (1 - t) * t * 214 + t * t * 204, (1 - t) ** 2 * 124 + 2 * (1 - t) * t * 112 + t * t * 172)
             for t in np.linspace(0, 1, 30)]
    c.stroke(curve, 7, dash=12)
    c.arrow_head(curve[-1], math.degrees(math.atan2(curve[-1][1] - curve[-3][1], curve[-1][0] - curve[-3][0])), 24, 8)
    c.stroke(c.circle_pts(204, 212, 30), 7, closed=True)
    c.fill(c.circle_pts(204, 212, 10), opacity=0.9)
    c.save("Icons/Icon_EagleEye.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 47)  # Домино: костяшки падают одна на другую
    c.stroke([(12, 224), (244, 224)], 8)
    for x, angle in ((50, 50), (122, 25), (196, 0)):
        tile = rotate(c.round_rect_pts(x - 20, 124, x + 20, 220, 8), x + 20, 220, angle)
        c.fill(tile, opacity=0.35, hatch=(-30, 8))
        c.stroke(tile, 8, closed=True)
        c.stroke(rotate([(x - 12, 172), (x + 12, 172)], x + 20, 220, angle), 5)
        for dy in (148, 196):
            dot = rotate(c.circle_pts(x, dy, 6), x + 20, 220, angle)
            c.fill(dot, opacity=0.9)
    for x0, y0 in ((40, 100), (30, 124)):
        c.stroke([(x0, y0), (x0 - 20, y0 + 6)], 6, opacity=0.8)
    c.save("Icons/Icon_Domino.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 48)  # Копилка: свинка-копилка и монетка над щелью
    body = [(124 + 82 * math.cos(a), 158 + 56 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)]
    c.fill(body, opacity=0.35, hatch=(30, 9))
    c.stroke(body, 9, closed=True)
    snout = c.circle_pts(210, 160, 18)
    c.stroke(snout, 8, closed=True)
    for y in (154, 168):
        c.fill(c.circle_pts(210, y, 4), opacity=0.9)
    c.stroke([(92, 110), (108, 78), (126, 104)], 8)
    c.fill(c.circle_pts(172, 136, 6), opacity=0.9)
    for x in (72, 104, 148, 176):
        c.stroke([(x, 206), (x, 230)], 12)
    c.stroke([(44, 150), (30, 142), (24, 154), (34, 162), (28, 172)], 6)
    c.stroke([(106, 104), (148, 104)], 7)
    coin = c.circle_pts(128, 42, 24)
    c.fill(coin, opacity=0.4, hatch=(30, 7))
    c.stroke(coin, 7, closed=True)
    for y in (34, 42):
        c.stroke([(116, y), (140, y)], 5)
    c.stroke([(128, 42), (128, 56)], 5)
    c.save("Icons/Icon_Piggy.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 49)  # Крышка от кастрюли: мяч отскакивает от крышки, как от щита
    lid = c.circle_pts(108, 142, 80)
    c.fill(lid, opacity=0.3, hatch=(30, 9))
    c.stroke(lid, 9, closed=True)
    c.stroke(c.circle_pts(108, 142, 60), 5, closed=True, opacity=0.7)
    knob = c.round_rect_pts(90, 130, 126, 154, 10)
    c.fill(knob, opacity=0.8)
    c.stroke(knob, 6, closed=True)
    c.stroke(dense([(190, 112), (214, 64)]), 7, dash=11)
    for a in (-60, -20, 20):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(190 + ca * 12, 118 + sa * 12), (190 + ca * 28, 118 + sa * 28)], 6)
    ball = c.circle_pts(220, 44, 22)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Lid.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 50)  # Йо-йо: йо-йо на нитке крутится и возвращается в руку
    yoyo = c.circle_pts(128, 170, 58)
    c.fill(yoyo, opacity=0.4, hatch=(30, 9))
    c.stroke(yoyo, 9, closed=True)
    c.stroke(c.circle_pts(128, 170, 40), 5, closed=True, opacity=0.7)
    c.fill(c.circle_pts(128, 170, 10), opacity=0.9)
    c.stroke([(128, 162), (128, 36)], 5)
    c.stroke(c.circle_pts(128, 24, 13), 8, closed=True)
    back = c.circle_pts(128, 170, 80, 200, 290, 24)
    c.stroke(back, 7)
    c.arrow_head(back[-1], math.degrees(math.atan2(back[-1][1] - back[-3][1], back[-1][0] - back[-3][0])), 22, 7)
    c.stroke(c.circle_pts(128, 170, 80, 20, 110, 24), 7, opacity=0.8)
    c.save("Icons/Icon_Yoyo.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 51)  # Скакалка: верёвка дугой над головой, две ручки и полоски движения
    rope = [(64 + 128 * t, 196 - 170 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 40)]
    c.stroke(rope, 7)
    for x, a in ((64, 20), (192, -20)):
        handle = rotate(c.round_rect_pts(x - 11, 192, x + 11, 244, 9), x, 196, a)
        c.fill(handle, opacity=0.45, hatch=(30, 8))
        c.stroke(handle, 8, closed=True)
    for y, x0 in ((70, 16), (100, 8), (130, 16)):
        c.stroke([(x0, y), (x0 + 30, y)], 6, opacity=0.8)
        c.stroke([(256 - x0, y), (226 - x0, y)], 6, opacity=0.8)
    c.save("Icons/Icon_JumpRope.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 52)  # Зеркальце: овальное зеркальце с ручкой, в нём глаз, по стеклу блики
    glass = [(128 + 60 * math.cos(a), 100 + 78 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 64, endpoint=False)]
    c.fill(glass, opacity=0.15, hatch=(45, 12))
    c.stroke(glass, 11, closed=True)
    c.stroke([(128 + 46 * math.cos(a), 100 + 63 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 64, endpoint=False)],
             5, closed=True, opacity=0.7)
    c.stroke(c.circle_pts(128, 18, 9), 6, closed=True)
    handle = c.round_rect_pts(116, 180, 140, 248, 10)
    c.fill(handle, opacity=0.45, hatch=(30, 8))
    c.stroke(handle, 8, closed=True)
    upper = [(98 + 60 * t, 112 - 18 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    lower = [(158 - 60 * t, 112 + 16 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    c.stroke(upper + lower, 6, closed=True)
    c.fill(c.circle_pts(128, 112, 9), opacity=0.9)
    for x0, y0, x1, y1 in ((96, 70, 118, 48), (96, 90, 132, 54)):
        c.stroke([(x0, y0), (x1, y1)], 6, opacity=0.85)
    c.save("Icons/Icon_Mirror.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 53)  # Фонарик: фонарик светит широким конусом
    body = rotate(c.round_rect_pts(22, 108, 104, 148, 10), 64, 128, 0)
    c.fill(body, opacity=0.45, hatch=(30, 8))
    c.stroke(body, 9, closed=True)
    head = [(104, 100), (132, 88), (132, 168), (104, 156)]
    c.fill(head, opacity=0.3, hatch=(-30, 8))
    c.stroke(head, 9, closed=True)
    c.stroke([(60, 108), (60, 96), (76, 96), (76, 108)], 6)
    for a in (-24, -8, 8, 24):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke(dense([(140 + ca * 10, 128 + sa * 10), (140 + ca * 104, 128 + sa * 104)]), 6, dash=14)
    c.stroke(c.circle_pts(140, 128, 110, -30, 30, 16), 7)
    c.save("Icons/Icon_Flashlight.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 54)  # Свисток: свисток на шнурке, от него волны звука и снежинка — все замерли
    whistle = [(46, 118), (150, 118), (150, 110), (176, 110), (176, 134)]
    whistle += [(148 + 34 * math.cos(a), 150 + 34 * math.sin(a)) for a in np.linspace(math.radians(-20), math.radians(200), 20)]
    whistle += [(46, 150)]
    c.fill(whistle, opacity=0.45, hatch=(30, 8))
    c.stroke(whistle, 9, closed=True)
    c.stroke(c.circle_pts(148, 150, 12), 5, closed=True)
    c.stroke([(52, 118), (70, 60), (120, 34)], 5, opacity=0.85)
    for r in (26, 44):
        c.stroke(c.circle_pts(196, 150, r, -40, 40, 10), 6)
    cx, cy = 214, 52
    for k in range(3):
        a = math.radians(90 + k * 60)
        c.stroke([(cx - math.cos(a) * 22, cy + math.sin(a) * 22), (cx + math.cos(a) * 22, cy - math.sin(a) * 22)], 6)
    c.save("Icons/Icon_Whistle.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 55)  # Второе дыхание: сердце с пластырем крест-накрест и искры
    heart = c.heart_pts(128, 132, 6.2)
    c.fill(heart, opacity=0.35, hatch=(30, 9))
    c.stroke(heart, 10, closed=True)
    for a in (35, -35):
        plaster = rotate(c.round_rect_pts(84, 116, 172, 144, 10), 128, 130, a)
        c.fill(plaster, opacity=0.2)
        c.stroke(plaster, 7, closed=True)
    for x, y in ((40, 40), (216, 44), (224, 96)):
        c.stroke([(x - 12, y), (x + 12, y)], 6)
        c.stroke([(x, y - 12), (x, y + 12)], 6)
    c.save("Icons/Icon_SecondWind.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 56)  # Бабушкины пирожки: два пирожка со швом, над ними пар
    for cx, cy, a in ((96, 178, -8), (160, 150, 10)):
        pie = rotate([(cx + 62 * math.cos(t), cy + 30 * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 50, endpoint=False)], cx, cy, a)
        c.fill(pie, opacity=0.35, hatch=(30, 9))
        c.stroke(pie, 9, closed=True)
        seam = rotate([(cx - 44 + 88 * t, cy - 14 + 4 * math.sin(t * 14)) for t in np.linspace(0, 1, 30)], cx, cy, a)
        c.stroke(seam, 5)
    for x in (100, 136, 172):
        c.stroke([(x + 9 * math.sin(i * 1.1), 108 - i * 12) for i in range(7)], 7)
    c.save("Icons/Icon_Pies.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 57)  # Резиновые сапоги: сапог шлёпает по луже, брызги
    boot = [(92, 30), (150, 30), (150, 150), (212, 172), (220, 206), (80, 206), (80, 150)]
    c.fill(boot, opacity=0.4, hatch=(-30, 9))
    c.stroke(boot, 9, closed=True)
    c.stroke([(92, 60), (150, 60)], 6)
    c.stroke([(80, 190), (220, 190)], 6)
    splat = [(128 + 110 * math.cos(a), 226 + 16 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 50, endpoint=False)]
    c.stroke(splat, 7, closed=True)
    for x0, y0, x1, y1 in ((30, 200, 12, 172), (44, 190, 36, 158), (226, 196, 244, 168)):
        c.stroke([(x0, y0), (x1, y1)], 6)
        c.fill(c.circle_pts(x1, y1 - 8, 5), opacity=0.9)
    c.save("Icons/Icon_Boots.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 58)  # Прыгающая бомба: мяч скачет, и на каждом отскоке — взрыв
    c.stroke([(8, 222), (248, 222)], 8)
    path = [(20 + 90 * t, 214 - 150 * 4 * t * (1 - t)) for t in np.linspace(0, 1, 24)]
    path += [(110 + 70 * t, 214 - 110 * 4 * t * (1 - t)) for t in np.linspace(0, 1, 20)][1:]
    c.stroke(path, 6, dash=12)
    for x in (110, 180):
        for a in range(0, 360, 45):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            r0, r1 = (10, 26) if a % 90 == 0 else (8, 18)
            if sa > 0.3:
                continue
            c.stroke([(x + ca * r0, 214 + sa * r0), (x + ca * r1, 214 + sa * r1)], 6)
    ball = c.circle_pts(214, 118, 26)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke([(226, 94), (238, 70)], 6)
    c.stroke(c.circle_pts(242, 62, 7), 5, closed=True)
    c.save("Icons/Icon_BounceBomb.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 59)  # Град: теннисный мяч раскалывается на мячики, мячики — ещё раз
    ball = c.circle_pts(62, 128, 32)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke(c.circle_pts(40, 128, 30, -60, 60, 12), 5)
    for tip, kids in (((150, 70), ((214, 36), (222, 92))), ((150, 186), ((214, 164), (222, 220)))):
        c.stroke([(96, 128), tip], 6)
        b = c.circle_pts(tip[0], tip[1], 18)
        c.fill(b, opacity=0.5, hatch=(30, 7))
        c.stroke(b, 7, closed=True)
        for k in kids:
            c.stroke([(tip[0] + 18, tip[1]), (k[0] - 12, k[1])], 5, opacity=0.85)
            kb = c.circle_pts(k[0], k[1], 11)
            c.fill(kb, opacity=0.7)
            c.stroke(kb, 5, closed=True)
    c.save("Icons/Icon_Hail.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 60)  # Гиря: чугунная гиря бьёт в пол, вокруг звёздочки — все оглушены
    bell = [(128 + 70 * math.cos(a), 168 + 58 * math.sin(a)) for a in np.linspace(math.radians(-60), math.radians(240), 40)]
    c.fill(bell, opacity=0.45, hatch=(30, 9))
    c.stroke(bell, 10, closed=True)
    c.stroke(c.circle_pts(128, 96, 44, 180, 360, 24), 14, rough=0.8)
    c.stroke([(84, 96), (92, 124)], 12)
    c.stroke([(172, 96), (164, 124)], 12)
    c.stroke([(16, 236), (240, 236)], 8)
    for x, y in ((32, 110), (224, 110), (40, 190), (216, 196)):
        for k in range(5):
            a = math.radians(-90 + k * 72)
            c.stroke([(x, y), (x + math.cos(a) * 14, y + math.sin(a) * 14)], 5)
    c.save("Icons/Icon_Kettlebell.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 61)  # Кувырок: кед в рывке по дуге подхватывает летящий мяч
    arc = c.circle_pts(128, 150, 88, 190, 350, 40)
    c.stroke(arc, 8, dash=14)
    c.arrow_head(arc[-1], math.degrees(math.atan2(arc[-1][1] - arc[-3][1], arc[-1][0] - arc[-3][0])), 28, 8)
    shoe = [(70, 190), (70, 138), (104, 128), (130, 96), (162, 96), (170, 134), (228, 152), (236, 190)]
    shoe = [(74 + (x - 150) * 0.55, 196 + (y - 150) * 0.55) for x, y in shoe]
    c.fill(shoe, opacity=0.35, hatch=(-30, 9))
    c.stroke(shoe, 8, closed=True)
    ball = c.circle_pts(128, 70, 26)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke(c.circle_pts(128, 70, 44, 110, 250, 14), 6)
    c.stroke(c.circle_pts(128, 70, 44, -70, 70, 14), 6)
    c.save("Icons/Icon_Roll.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 62)  # Шпаргалка: сложенная бумажка со строчками и галочкой
    sheet = [(56, 30), (190, 30), (214, 54), (214, 226), (56, 226)]
    c.fill(sheet, opacity=0.2, hatch=(45, 12))
    c.stroke(sheet, 9, closed=True)
    c.stroke([(190, 30), (190, 54), (214, 54)], 7)
    for i, y in enumerate((80, 112, 144, 176)):
        c.stroke(wobble(dense([(80, y), (186 - (i % 2) * 30, y)], 8), 2.0, 62 + i), 5)
    c.stroke([(150, 200), (170, 218), (232, 152)], 10)
    c.save("Icons/Icon_CheatSheet.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 63)  # Счастливый фантик: фантик-конфета, на нём клевер-четырёхлистник
    wrap = [(40, 90), (84, 106), (172, 106), (216, 90), (206, 128), (216, 166), (172, 150), (84, 150), (40, 166), (50, 128)]
    c.fill(wrap, opacity=0.25, hatch=(30, 10))
    c.stroke(wrap, 9, closed=True)
    c.stroke([(84, 106), (84, 150)], 6)
    c.stroke([(172, 106), (172, 150)], 6)
    for dx, dy in ((0, -14), (14, 0), (0, 14), (-14, 0)):
        leaf = c.circle_pts(128 + dx, 128 + dy, 11)
        c.fill(leaf, opacity=0.8)
    c.stroke([(128, 128), (138, 152)], 5)
    for x, y in ((56, 44), (200, 40), (224, 214), (40, 214)):
        for k in range(4):
            a = math.radians(k * 45)
            c.stroke([(x - math.cos(a) * 11, y - math.sin(a) * 11), (x + math.cos(a) * 11, y + math.sin(a) * 11)], 5)
    c.save("Icons/Icon_LuckyWrapper.png", grain=0.3, color=INK)


if __name__ == "__main__":
    hud()
    card_frames()
    icons()
