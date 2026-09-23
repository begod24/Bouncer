#!/usr/bin/env python3
"""
Bouncer: UI-спрайты в стиле «мел на асфальте» и дудл-иконки для карточек-вкладышей.

Запуск из корня репозитория:  python3 Tools/ui_art.py
Нужны numpy и Pillow. HUD белый — цвет задаётся в Unity (Image.color); иконки вкладышей тёмные.
Иконки мячей (волейбольный, набивной, теннисный) рендерит Unity из 3D-моделей, их здесь нет.
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


if __name__ == "__main__":
    hud()
    card_frames()
    icons()
