-- Patient Management Demo - Database Initialization Script
-- PostgreSQL 16

CREATE TABLE IF NOT EXISTS patients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    gender VARCHAR(10) NOT NULL,
    date_of_birth DATE NOT NULL,
    phone VARCHAR(20) NOT NULL,
    address VARCHAR(200),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed data
INSERT INTO patients (name, gender, date_of_birth, phone, address) VALUES
    ('张伟', '男', '1985-03-15', '13800138001', '北京市朝阳区建国路88号'),
    ('李娜', '女', '1990-07-22', '13900139002', '上海市浦东新区陆家嘴路100号'),
    ('王芳', '女', '1978-11-08', '13700137003', '广州市天河区天河路385号'),
    ('刘洋', '男', '2000-01-30', '13600136004', '成都市锦江区红星路三段1号'),
    ('陈静', '女', '1995-05-20', '13500135005', NULL);
