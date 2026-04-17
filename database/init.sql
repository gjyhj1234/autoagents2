-- Patient Management Demo
-- Database initialization script for PostgreSQL 16

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

-- Seed data (idempotent: fixed UUIDs prevent duplicate rows on re-run)
INSERT INTO patients (id, name, gender, date_of_birth, phone, address)
VALUES
    ('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', '张伟', '男', '1985-03-12', '13800138001', '北京市朝阳区建国路88号'),
    ('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a12', '李芳', '女', '1990-07-24', '13900139002', '上海市浦东新区陆家嘴环路1000号'),
    ('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a13', '王磊', '男', '1978-11-05', '13700137003', '广州市天河区珠江新城花城大道'),
    ('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a14', '陈静', '女', '2000-01-30', '13600136004', '成都市武侯区天府大道南段666号'),
    ('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a15', '刘洋', '男', '1995-09-18', '13500135005', NULL)
ON CONFLICT (id) DO NOTHING;
